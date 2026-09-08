package com.pianoglass.midi;

import android.content.Context;
import android.content.pm.PackageManager;
import android.media.midi.MidiDevice;
import android.media.midi.MidiDeviceInfo;
import android.media.midi.MidiManager;
import android.media.midi.MidiOutputPort;
import android.media.midi.MidiReceiver;
import android.os.Handler;
import android.os.HandlerThread;
import android.util.Log;

import java.io.IOException;

/**
 * PianoGlass — Android MIDI input bridge for Unity.
 *
 * Why this file exists at all:
 *   Unity's AndroidJavaProxy can only implement Java *interfaces*, and
 *   android.media.midi.MidiReceiver is an abstract *class*. So the receiving
 *   end has to live in Java. Everything else stays in C#.
 *
 * Threading:
 *   MIDI callbacks arrive on a background Handler thread. Touching any Unity
 *   object from there will crash you in confusing ways, so this class does the
 *   absolute minimum on that thread: parse bytes, push ints into a ring buffer.
 *   Unity drains the buffer on the main thread once per frame via drain().
 *
 * Event packing:
 *   drain() returns an int[] with 4 ints per event:
 *     [0] status  (0x90 = note on ch1, 0x80 = note off ch1, ...)
 *     [1] data1   (note number, 0-127)
 *     [2] data2   (velocity, 0-127)
 *     [3] timestamp in milliseconds since init()
 *   Returns null when there is nothing to report, so the common case allocates
 *   nothing at all.
 */
public final class MidiBridge {

    private static final String TAG = "PianoGlassMidi";

    /** Max events buffered between Unity frames. 512 events is ~8 seconds of
     *  very fast playing; anything beyond that means Unity has stalled. */
    private static final int CAPACITY = 512;
    private static final int STRIDE = 4;

    private static final Object LOCK = new Object();

    private static final int[] ring = new int[CAPACITY * STRIDE];
    private static int head = 0;      // next write slot (in events)
    private static int count = 0;     // events currently buffered
    private static long droppedEvents = 0;

    private static MidiManager midiManager;
    private static HandlerThread handlerThread;
    private static Handler handler;
    private static MidiDevice openDevice;
    private static MidiOutputPort openPort;
    private static MidiReceiver receiver;

    private static volatile boolean initialised = false;
    private static volatile boolean midiFeatureAvailable = false;
    private static volatile String deviceName = "";
    private static volatile String lastError = "";
    private static long startNanos = 0L;

    private MidiBridge() { }

    // ------------------------------------------------------------------
    // Public API — everything below is called from C# via AndroidJavaClass
    // ------------------------------------------------------------------

    /**
     * Start listening. Safe to call more than once.
     * @param context the Unity activity (pass currentActivity from C#)
     * @return true if the MIDI service is available on this device
     */
    public static boolean init(Context context) {
        synchronized (LOCK) {
            if (initialised) return midiFeatureAvailable;

            startNanos = System.nanoTime();
            lastError = "";

            PackageManager pm = context.getPackageManager();
            midiFeatureAvailable = pm.hasSystemFeature(PackageManager.FEATURE_MIDI);
            if (!midiFeatureAvailable) {
                lastError = "This device does not report android.software.midi";
                Log.w(TAG, lastError);
                initialised = true;
                return false;
            }

            midiManager = (MidiManager) context.getSystemService(Context.MIDI_SERVICE);
            if (midiManager == null) {
                midiFeatureAvailable = false;
                lastError = "MIDI_SERVICE was null";
                Log.e(TAG, lastError);
                initialised = true;
                return false;
            }

            handlerThread = new HandlerThread("PianoGlassMidi");
            handlerThread.start();
            handler = new Handler(handlerThread.getLooper());

            receiver = new ParsingReceiver();

            // Devices already plugged in when the app starts.
            for (MidiDeviceInfo info : midiManager.getDevices()) {
                tryOpen(info);
            }

            // Devices plugged in later.
            midiManager.registerDeviceCallback(new MidiManager.DeviceCallback() {
                @Override
                public void onDeviceAdded(MidiDeviceInfo info) {
                    Log.i(TAG, "MIDI device added: " + nameOf(info));
                    tryOpen(info);
                }

                @Override
                public void onDeviceRemoved(MidiDeviceInfo info) {
                    Log.i(TAG, "MIDI device removed: " + nameOf(info));
                    if (openDevice != null && openDevice.getInfo().getId() == info.getId()) {
                        closePort();
                    }
                }
            }, handler);

            initialised = true;
            return true;
        }
    }

    /** True if a MIDI device is currently open and connected. */
    public static boolean isConnected() {
        return openPort != null;
    }

    /** True if the OS exposes the MIDI service at all. False here means the
     *  UsbManager fallback route is the only option on this headset. */
    public static boolean isSupported() {
        return midiFeatureAvailable;
    }

    /** Human-readable line for the debug HUD. */
    public static String getStatus() {
        if (!initialised) return "not initialised";
        if (!midiFeatureAvailable) return "NO MIDI SERVICE — " + lastError;
        if (openPort == null) return "waiting for a keyboard...";
        String s = "connected: " + deviceName;
        if (droppedEvents > 0) s += "  (dropped " + droppedEvents + ")";
        return s;
    }

    /**
     * Take everything buffered since the last call.
     * @return int[] of 4-int events, or null when empty.
     */
    public static int[] drain() {
        synchronized (LOCK) {
            if (count == 0) return null;

            int[] out = new int[count * STRIDE];
            int start = head - count;
            if (start < 0) start += CAPACITY;

            for (int i = 0; i < count; i++) {
                int src = ((start + i) % CAPACITY) * STRIDE;
                int dst = i * STRIDE;
                out[dst]     = ring[src];
                out[dst + 1] = ring[src + 1];
                out[dst + 2] = ring[src + 2];
                out[dst + 3] = ring[src + 3];
            }
            count = 0;
            return out;
        }
    }

    /** Call from OnDestroy / OnApplicationQuit. */
    public static void shutdown() {
        synchronized (LOCK) {
            closePort();
            if (handlerThread != null) {
                handlerThread.quitSafely();
                handlerThread = null;
                handler = null;
            }
            midiManager = null;
            initialised = false;
            count = 0;
            head = 0;
        }
    }

    // ------------------------------------------------------------------
    // Device handling
    // ------------------------------------------------------------------

    private static String nameOf(MidiDeviceInfo info) {
        String n = info.getProperties().getString(MidiDeviceInfo.PROPERTY_NAME);
        if (n == null) n = info.getProperties().getString(MidiDeviceInfo.PROPERTY_PRODUCT);
        return n == null ? ("device " + info.getId()) : n;
    }

    private static void tryOpen(final MidiDeviceInfo info) {
        if (openPort != null) return;                       // one keyboard is enough
        if (info.getType() == MidiDeviceInfo.TYPE_VIRTUAL) return;

        // NOTE the direction: to *receive* notes from a keyboard we connect to
        // that device's OUTPUT port. TYPE_INPUT is where you would send data to
        // the device. Getting this backwards is the classic silent failure.
        int portNumber = -1;
        for (MidiDeviceInfo.PortInfo port : info.getPorts()) {
            if (port.getType() == MidiDeviceInfo.PortInfo.TYPE_OUTPUT) {
                portNumber = port.getPortNumber();
                break;
            }
        }
        if (portNumber < 0) {
            Log.i(TAG, "Ignoring " + nameOf(info) + " — no output port");
            return;
        }

        final int finalPort = portNumber;
        midiManager.openDevice(info, new MidiManager.OnDeviceOpenedListener() {
            @Override
            public void onDeviceOpened(MidiDevice device) {
                if (device == null) {
                    lastError = "openDevice returned null for " + nameOf(info);
                    Log.e(TAG, lastError);
                    return;
                }
                synchronized (LOCK) {
                    if (openPort != null) {                 // raced with another device
                        try { device.close(); } catch (IOException ignored) { }
                        return;
                    }
                    MidiOutputPort port = device.openOutputPort(finalPort);
                    if (port == null) {
                        lastError = "openOutputPort(" + finalPort + ") failed";
                        Log.e(TAG, lastError);
                        try { device.close(); } catch (IOException ignored) { }
                        return;
                    }
                    port.connect(receiver);
                    openDevice = device;
                    openPort = port;
                    deviceName = nameOf(info);
                    Log.i(TAG, "MIDI connected: " + deviceName + " port " + finalPort);
                }
            }
        }, handler);
    }

    private static void closePort() {
        try {
            if (openPort != null) {
                openPort.disconnect(receiver);
                openPort.close();
            }
        } catch (IOException e) {
            Log.w(TAG, "closing port: " + e.getMessage());
        }
        try {
            if (openDevice != null) openDevice.close();
        } catch (IOException e) {
            Log.w(TAG, "closing device: " + e.getMessage());
        }
        openPort = null;
        openDevice = null;
        deviceName = "";
    }

    // ------------------------------------------------------------------
    // Byte-level parsing
    // ------------------------------------------------------------------

    /**
     * A USB-MIDI stream does not arrive as tidy 3-byte messages. Bytes can be
     * split across callbacks, real-time bytes (clock, active sensing) can be
     * injected mid-message, and "running status" lets a keyboard omit the
     * status byte on repeated messages. This is a proper little state machine
     * so none of that surprises you at 2am.
     */
    private static final class ParsingReceiver extends MidiReceiver {

        private int status = 0;
        private int data1 = 0;
        private boolean haveData1 = false;
        private boolean inSysEx = false;

        @Override
        public void onSend(byte[] data, int offset, int length, long timestampNanos) {
            int timeMs = (int) ((System.nanoTime() - startNanos) / 1_000_000L);

            for (int i = offset; i < offset + length; i++) {
                int b = data[i] & 0xFF;

                if (b >= 0xF8) continue;              // system real-time: ignore
                if (b == 0xF0) { inSysEx = true;  continue; }
                if (b == 0xF7) { inSysEx = false; continue; }
                if (inSysEx) continue;

                if (b >= 0x80) {                      // status byte
                    if (b >= 0xF0) {                  // system common: reset and ignore
                        status = 0;
                        haveData1 = false;
                    } else {
                        status = b;
                        haveData1 = false;
                    }
                    continue;
                }

                if (status == 0) continue;            // data with no status yet

                if (!haveData1) {
                    data1 = b;
                    int type = status & 0xF0;
                    if (type == 0xC0 || type == 0xD0) {
                        push(status, data1, 0, timeMs);   // 2-byte messages
                    } else {
                        haveData1 = true;
                    }
                    continue;
                }

                push(status, data1, b, timeMs);
                haveData1 = false;                    // running status keeps `status`
            }
        }
    }

    private static void push(int status, int d1, int d2, int timeMs) {
        synchronized (LOCK) {
            if (count >= CAPACITY) {
                droppedEvents++;
                return;
            }
            int slot = head * STRIDE;
            ring[slot]     = status;
            ring[slot + 1] = d1;
            ring[slot + 2] = d2;
            ring[slot + 3] = timeMs;
            head = (head + 1) % CAPACITY;
            count++;
        }
    }
}
