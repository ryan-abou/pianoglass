# Traps already hit

Full text, moved out of CLAUDE.md on 2026-11-07. CLAUDE.md keeps one line per trap.


- **Unity does not compile a `.java` file until you select it in the Project
  window and tick Android under "Select platforms for plugin".** Toggle it off,
  Apply, on, Apply to force the `PluginImporter` block to serialize. Without
  it the build succeeds and fails at runtime with `ClassNotFoundException`.
- **A Screen Space - Overlay canvas is invisible in VR.** World Space, and
  scaled to ~0.001 or it is a 600-metre wall. Cost a full build cycle.
- **A world-space Canvas left at a fixed world position, y = 0, is at floor
  height in Quest tracking space** — unreadably low, and easy to mistake for
  a broken build rather than a placement bug. `CanvasCalibrationFollower`
  registers it on the calibrated keyboard instead, the same way `NoteHighway`
  registers the note wall; before calibration exists it falls back to a
  serialized height rather than whatever y the Canvas happened to be authored
  at.
- **A running adb server breaks headless Unity. Run `adb kill-server` before
  every batchmode run** — tests, a `SceneSetup` method via `-executeMethod`,
  and `build.ps1`. With an adb server already up, batchmode Unity exits
  `-1073740791` (`0xC0000409`) right after "Native extension for Android
  target not found", before any test or method runs — it looks like a code or
  scene bug and is neither. Unity's Android module starts that adb server
  itself and it outlives Unity, so the *second* headless run after anything
  Android-related (including `build.ps1 -Install`) is the one that crashes.
  The same lingering adb child is why `build.ps1`'s `Start-Process -Wait` can
  hang long after Unity has exited: `-Wait` waits for the whole process tree.
  `-PassThru` plus `$p.WaitForExit()` waits for Unity alone.
- **Unity hides file extensions in the Project window.** Rename `.cs.txt` files
  in Explorer, not in Unity.
- **Do not overwrite or hand-edit `Assets/Plugins/Android/AndroidManifest.xml`.**
  The checked-in file has no passthrough, anchor or MIDI entries: Meta's
  Gradle step (`OVRManifestPreprocessor`) injects passthrough and
  `USE_ANCHOR_API` into the *merged* manifest at build time, from
  `OculusProjectConfig.asset`. Check the APK (`aapt dump permissions`), not
  the file. There is **no** `android.software.midi` `uses-feature` line and
  none is needed — MIDI works on device without it, and with
  `required="false"` it only ever affected store filtering, never runtime
  behaviour. `MANIFEST-additions.txt` says what the file holds and how to
  check the merged APK.
- **A MIDI note-on with velocity 0 is a note-off.** Most keyboards send these.
  Missed, every note sticks on forever.
- **To *receive* from a keyboard you open its OUTPUT port**, not its input port.
  Getting this backwards gives silence and no error.
- **Assign materials in the Inspector.** A runtime `Shader.Find` gets stripped
  from device builds and you see nothing, with no clue why.
- **`OVRInput.Button.PrimaryIndexTrigger` without a `Controller` argument**
  resolves against whichever controller is "active", and `Secondary...` may
  resolve to nothing. Always name `OVRInput.Controller.RTouch` / `LTouch`.
- **The first `DateTime.Now` on the headset takes about 380 ms.** It froze the
  first press of play for a visible moment until `ScoreKeeper` began doing it
  on a background thread at launch (measured 2026-11-05: longest frame after
  that press went from 390 ms to 28 ms). Any other first-use cost on the main
  thread will look the same, and it looks like a graphics stall when it is not:
  profile before guessing. In a development build, set `Profiler.logFile`,
  `enableBinaryLog` and `enabled`, pull the `.raw`, and read the longest frame
  with `ProfilerDriver.LoadProfile` and `RawFrameDataView`: it names the method.
- **`FindFirstObjectByType` is deprecated in 6.6** — use `FindAnyObjectByType`.
- **`git commit -am` ignores untracked files.** Any new Unity asset — script,
  material, prefab, scene — needs `git add -A`. Check `git status` first.
- **Meta XR Audio SDK 205.0.0 has a runtime script that references editor-only
  APIs** and can block player builds with `CS0234 UnityEditor.GUID`. Restarting
  the editor cleared it once. If it returns, replace `com.meta.xr.sdk.all` with
  `com.meta.xr.sdk.core` **plus `com.meta.xr.sdk.interaction`** (same
  version, 205.0.0) in `Packages/manifest.json`. Nothing here uses audio,
  platform or voice. Interaction is needed once the hand-tracked UI (phase 4)
  exists, and dropping it would break that.
- **A camera with `Skybox` clear flags renders an opaque sky over
  everything, passthrough included — this is what the "black void, can't see
  my hands" report traced back to.** Enabling `OVRManager.isInsightPassthroughEnabled`
  and adding an `OVRPassthroughLayer` does nothing if the camera still clears
  to `Skybox`: the skybox material draws over the passthrough layer every
  frame regardless, and if that material happens to render as black, the
  result looks exactly like passthrough silently failing rather than like a
  camera setting. Needs **both** halves: `clearFlags = SolidColor` with
  background alpha `0`, **and** `RenderSettings.skybox = null` — either one
  alone still leaves something opaque in front of the passthrough feed. Also
  check the active URP asset's HDR flag and any `Global Volume` with active
  post-processing overrides before assuming the camera is the whole story:
  HDR and post-processing can each rewrite the camera's alpha channel on the
  way out, which breaks the same compositing even with clear flags and
  skybox both correct. (Not a problem in this project today — the Android
  quality tier's `Mobile_RPAsset` has HDR off, and the scene's `Global
  Volume` has post-processing disabled on the XR camera — but re-check both
  if passthrough ever goes transparent-but-black again after someone enables
  post-processing or switches render pipeline assets.)

---

