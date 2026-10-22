using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace PianoGlass.Piano
{
    /// <summary>
    /// IPianoAnchorService on Meta XR SDK 205.0.0's OVRSpatialAnchor. The only
    /// file that touches OVRSpatialAnchor.
    ///
    /// Uses only the async, non-[Obsolete] members, checked against
    /// com.meta.xr.sdk.core 205.0.0's OVRSpatialAnchor.cs / OVRAnchor.cs:
    ///
    ///   create   AddComponent&lt;OVRSpatialAnchor&gt;() + WhenCreatedAsync()  -> OVRTask&lt;bool&gt;
    ///   save     SaveAnchorAsync()            -> OVRTask&lt;OVRResult&lt;OVRAnchor.SaveResult&gt;&gt;
    ///   load     LoadUnboundAnchorsAsync(uuids, buffer)
    ///                                         -> OVRTask&lt;OVRResult&lt;List&lt;UnboundAnchor&gt;, OVRAnchor.FetchResult&gt;&gt;
    ///            UnboundAnchor.LocalizeAsync(timeout) -> OVRTask&lt;bool&gt;
    ///            UnboundAnchor.BindTo(component)  -- same frame as AddComponent
    ///   erase    EraseAnchorAsync() / static EraseAnchorsAsync(anchors, uuids)
    ///                                         -> OVRTask&lt;OVRResult&lt;OVRAnchor.EraseResult&gt;&gt;
    ///
    /// Why the bind must be in the same frame: OVRSpatialAnchor.Start() creates
    /// a brand-new anchor at the GameObject's pose unless it is already bound.
    /// AddComponent and BindTo are back to back below with no await between
    /// them, so a load can never create an anchor by accident.
    ///
    /// Nothing here erases on a failure path. A load that comes back empty is
    /// NotFound; a localize that does not finish is Timeout -- the anchor is
    /// left in storage for the next attempt either way.
    /// </summary>
    public sealed class OvrPianoAnchorService : IPianoAnchorService
    {
        /// <summary>Create is normally well under a second; this only stops a
        /// runtime that never answers from hanging the caller.</summary>
        public double CreateTimeoutSeconds = 10;

        private readonly Dictionary<string, OvrAnchorHandle> _live = new Dictionary<string, OvrAnchorHandle>();
        private readonly List<OVRSpatialAnchor.UnboundAnchor> _buffer = new List<OVRSpatialAnchor.UnboundAnchor>();

        public async Task<AnchorResult> CreateAsync(Vector3 position, Quaternion rotation)
        {
            var sw = Stopwatch.StartNew();
            var go = new GameObject("PianoAnchor (creating)");
            go.transform.SetPositionAndRotation(position, rotation);
            OVRSpatialAnchor anchor = go.AddComponent<OVRSpatialAnchor>();

            bool created;
            try
            {
                Task<bool> creating = AsTask(anchor.WhenCreatedAsync());
                Task first = await Task.WhenAny(creating, Task.Delay(TimeSpan.FromSeconds(CreateTimeoutSeconds)));
                if (first != creating)
                {
                    // Destroying the component makes the SDK destroy the space
                    // if creation does complete later (OnSpatialAnchorCreateComplete).
                    Object.Destroy(go);
                    return new AnchorResult(AnchorStatus.Timeout, null, null,
                        $"WhenCreatedAsync did not finish in {CreateTimeoutSeconds:0}s", sw.ElapsedMilliseconds);
                }
                created = creating.Result;
            }
            catch (Exception e)
            {
                if (go) Object.Destroy(go);
                return new AnchorResult(AnchorStatus.Failed, null, null, e.GetType().Name + ": " + e.Message,
                                        sw.ElapsedMilliseconds);
            }

            if (!created || !anchor)
            {
                if (go) Object.Destroy(go);
                return new AnchorResult(AnchorStatus.Failed, null, null, "WhenCreatedAsync=false",
                                        sw.ElapsedMilliseconds);
            }

            OvrAnchorHandle handle = Register(anchor);
            return new AnchorResult(AnchorStatus.Success, handle.Id, handle, null, sw.ElapsedMilliseconds);
        }

        public async Task<AnchorResult> SaveAsync(IPianoAnchorHandle anchor)
        {
            var sw = Stopwatch.StartNew();
            if (!TryGetLive(anchor, out OvrAnchorHandle h))
                return new AnchorResult(AnchorStatus.InvalidArgument, anchor?.Id, null,
                                        "not a live handle from this service", 0);
            try
            {
                OVRResult<OVRAnchor.SaveResult> r = await h.Anchor.SaveAnchorAsync();
                return new AnchorResult(r.Success ? AnchorStatus.Success : AnchorStatus.Failed, h.Id, null,
                                        r.Status.ToString(), sw.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                return new AnchorResult(AnchorStatus.Failed, h.Id, null, e.GetType().Name + ": " + e.Message,
                                        sw.ElapsedMilliseconds);
            }
        }

        public async Task<AnchorResult> LoadAsync(string uuid, double localizeTimeoutSeconds)
        {
            var sw = Stopwatch.StartNew();
            if (string.IsNullOrEmpty(uuid) || !Guid.TryParse(uuid, out Guid guid))
                return new AnchorResult(AnchorStatus.InvalidArgument, uuid, null, "not a UUID", 0);

            // The SDK skips anchors already bound in this session, so a second
            // load would come back empty and look exactly like NotFound.
            if (_live.TryGetValue(uuid, out OvrAnchorHandle existing) && existing.IsAlive)
                return new AnchorResult(AnchorStatus.Success, uuid, existing, "already-bound", 0, 0);

            OVRSpatialAnchor.UnboundAnchor unbound;
            try
            {
                var load = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(new[] { guid }, _buffer);
                if (!load.Success)
                    return new AnchorResult(AnchorStatus.Failed, uuid, null, "fetch " + load.Status,
                                            sw.ElapsedMilliseconds);
                if (load.Value.Count == 0)
                    return new AnchorResult(AnchorStatus.NotFound, uuid, null, "fetch Success, 0 anchors",
                                            sw.ElapsedMilliseconds);
                unbound = load.Value[0];
            }
            catch (Exception e)
            {
                return new AnchorResult(AnchorStatus.Failed, uuid, null, e.GetType().Name + ": " + e.Message,
                                        sw.ElapsedMilliseconds);
            }

            long loadMs = sw.ElapsedMilliseconds;
            var lsw = Stopwatch.StartNew();
            bool localized;
            try
            {
                localized = unbound.Localized || await unbound.LocalizeAsync(localizeTimeoutSeconds);
            }
            catch (Exception e)
            {
                return new AnchorResult(AnchorStatus.Failed, uuid, null,
                    $"fetch {loadMs}ms, localize threw {e.GetType().Name}: {e.Message}",
                    sw.ElapsedMilliseconds, lsw.ElapsedMilliseconds);
            }

            if (!localized)
                return new AnchorResult(AnchorStatus.Timeout, uuid, null,
                    $"fetch {loadMs}ms, LocalizeAsync=false (timeout {localizeTimeoutSeconds:0.#}s)",
                    sw.ElapsedMilliseconds, lsw.ElapsedMilliseconds);

            // AddComponent and BindTo together, no await between: see class remarks.
            var go = new GameObject("PianoAnchor " + uuid);
            OVRSpatialAnchor anchor = go.AddComponent<OVRSpatialAnchor>();
            try
            {
                unbound.BindTo(anchor);
            }
            catch (Exception e)
            {
                Object.Destroy(go);
                return new AnchorResult(AnchorStatus.Failed, uuid, null,
                    $"BindTo threw {e.GetType().Name}: {e.Message}", sw.ElapsedMilliseconds, lsw.ElapsedMilliseconds);
            }

            OvrAnchorHandle handle = Register(anchor);
            return new AnchorResult(AnchorStatus.Success, uuid, handle, $"fetch {loadMs}ms",
                                    sw.ElapsedMilliseconds, lsw.ElapsedMilliseconds);
        }

        public async Task<AnchorResult> EraseAsync(IPianoAnchorHandle anchor)
        {
            var sw = Stopwatch.StartNew();
            if (!TryGetLive(anchor, out OvrAnchorHandle h))
                return new AnchorResult(AnchorStatus.InvalidArgument, anchor?.Id, null,
                                        "not a live handle from this service", 0);
            try
            {
                OVRResult<OVRAnchor.EraseResult> r = await h.Anchor.EraseAnchorAsync();
                if (r.Success) Forget(h);
                return new AnchorResult(r.Success ? AnchorStatus.Success : AnchorStatus.Failed, h.Id, null,
                                        r.Status.ToString(), sw.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                return new AnchorResult(AnchorStatus.Failed, h.Id, null, e.GetType().Name + ": " + e.Message,
                                        sw.ElapsedMilliseconds);
            }
        }

        public async Task<AnchorResult> EraseByUuidAsync(string uuid)
        {
            if (string.IsNullOrEmpty(uuid) || !Guid.TryParse(uuid, out Guid guid))
                return new AnchorResult(AnchorStatus.InvalidArgument, uuid, null, "not a UUID", 0);

            if (_live.TryGetValue(uuid, out OvrAnchorHandle bound) && bound.IsAlive)
                return await EraseAsync(bound);

            var sw = Stopwatch.StartNew();
            try
            {
                // An empty array, not null: EraseAnchorsAsync enumerates
                // `anchors` without a null check of its own.
                OVRResult<OVRAnchor.EraseResult> r = await OVRSpatialAnchor.EraseAnchorsAsync(
                    Array.Empty<OVRSpatialAnchor>(), new[] { guid });
                return new AnchorResult(r.Success ? AnchorStatus.Success : AnchorStatus.Failed, uuid, null,
                                        r.Status.ToString(), sw.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                return new AnchorResult(AnchorStatus.Failed, uuid, null, e.GetType().Name + ": " + e.Message,
                                        sw.ElapsedMilliseconds);
            }
        }

        // ------------------------------------------------------------------

        private OvrAnchorHandle Register(OVRSpatialAnchor anchor)
        {
            var handle = new OvrAnchorHandle(anchor);
            anchor.gameObject.name = "PianoAnchor " + handle.Id;
            _live[handle.Id] = handle;
            return handle;
        }

        private void Forget(OvrAnchorHandle h)
        {
            _live.Remove(h.Id);
            if (h.Anchor) Object.Destroy(h.Anchor.gameObject);
        }

        private bool TryGetLive(IPianoAnchorHandle anchor, out OvrAnchorHandle h)
        {
            h = anchor as OvrAnchorHandle;
            return h != null && h.IsAlive && _live.TryGetValue(h.Id, out OvrAnchorHandle mine) && mine == h;
        }

        private static async Task<bool> AsTask(OVRTask<bool> task) => await task;

        private sealed class OvrAnchorHandle : IPianoAnchorHandle
        {
            public readonly OVRSpatialAnchor Anchor;

            public OvrAnchorHandle(OVRSpatialAnchor anchor)
            {
                Anchor = anchor;
                Id = anchor.Uuid.ToString("D");
            }

            public string Id { get; }

            // OVRSpatialAnchor.Update() writes the runtime's pose into the
            // transform every frame, so this follows the anchor through a
            // recenter.
            public Pose Pose => Anchor
                ? new Pose(Anchor.transform.position, Anchor.transform.rotation)
                : Pose.identity;

            public bool IsTracked => Anchor && Anchor.IsTracked;
            public bool IsAlive => Anchor;
        }
    }
}
