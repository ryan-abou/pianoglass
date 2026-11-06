using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// A spatial-anchor runtime with no Meta code in it, for the editor and
    /// EditMode tests. Behaves like the real one where it matters:
    ///
    ///   * Anchors are stored in a fixed "physical" frame. Unity world space
    ///     is that frame seen through <see cref="TrackingOffset"/>, so
    ///     <see cref="SimulateRecenter"/> moves every live anchor in world
    ///     space exactly as a Quest recenter would, and a stored world
    ///     position would be wrong afterwards.
    ///   * <see cref="EndSession"/> drops every live handle, like a relaunch;
    ///     saved anchors survive it, unsaved ones do not.
    ///   * A load of an unknown UUID is NotFound, not Failed.
    ///   * Load never creates an anchor (<see cref="CreateCount"/> proves it).
    ///
    /// Set a Force* field to make the next calls of that kind return that
    /// status instead; null means behave normally. Every call completes
    /// synchronously, so tests can read <c>.Result</c>.
    /// </summary>
    public sealed class EditorPianoAnchorService : IPianoAnchorService
    {
        public AnchorStatus? ForceCreate, ForceSave, ForceLoad, ForceErase;

        public int CreateCount { get; private set; }
        public int SaveCount { get; private set; }
        public int LoadCount { get; private set; }
        public int EraseCount { get; private set; }

        /// <summary>Physical frame -> Unity world. Identity until a recenter.</summary>
        public Pose TrackingOffset { get; private set; } = Pose.identity;

        /// <summary>UUIDs persisted to "device storage", with their physical pose.</summary>
        public IReadOnlyDictionary<string, Pose> Stored => _stored;

        private readonly Dictionary<string, Pose> _stored = new Dictionary<string, Pose>();
        private readonly Dictionary<string, FakeHandle> _live = new Dictionary<string, FakeHandle>();

        public Task<AnchorResult> CreateAsync(Vector3 position, Quaternion rotation)
        {
            CreateCount++;
            if (ForceCreate is AnchorStatus forced && forced != AnchorStatus.Success)
                return Done(forced, null, null, "forced");

            var handle = new FakeHandle(this, Guid.NewGuid().ToString("D"), ToPhysical(new Pose(position, rotation)));
            _live[handle.Id] = handle;
            return Done(AnchorStatus.Success, handle.Id, handle, null);
        }

        public Task<AnchorResult> SaveAsync(IPianoAnchorHandle anchor)
        {
            SaveCount++;
            if (!(anchor is FakeHandle h) || h.Owner != this || !h.IsAlive)
                return Done(AnchorStatus.InvalidArgument, anchor?.Id, null, "not a live handle from this service");
            if (ForceSave is AnchorStatus forced && forced != AnchorStatus.Success)
                return Done(forced, h.Id, null, "forced");

            _stored[h.Id] = h.Physical;
            return Done(AnchorStatus.Success, h.Id, null, null);
        }

        public Task<AnchorResult> LoadAsync(string uuid, double localizeTimeoutSeconds)
        {
            LoadCount++;
            if (string.IsNullOrEmpty(uuid) || !Guid.TryParse(uuid, out _))
                return Done(AnchorStatus.InvalidArgument, uuid, null, "not a UUID");
            if (ForceLoad is AnchorStatus forced && forced != AnchorStatus.Success)
                return Done(forced, uuid, null, "forced", localizeMs: forced == AnchorStatus.Timeout ? 0 : -1);

            if (_live.TryGetValue(uuid, out FakeHandle existing) && existing.IsAlive)
                return Done(AnchorStatus.Success, uuid, existing, "already-bound", localizeMs: 0);
            if (!_stored.TryGetValue(uuid, out Pose physical))
                return Done(AnchorStatus.NotFound, uuid, null, "no anchor with that UUID");

            var handle = new FakeHandle(this, uuid, physical);
            _live[uuid] = handle;
            return Done(AnchorStatus.Success, uuid, handle, null, localizeMs: 0);
        }

        public Task<AnchorResult> EraseAsync(IPianoAnchorHandle anchor)
        {
            if (!(anchor is FakeHandle h) || h.Owner != this)
            {
                EraseCount++;
                return Done(AnchorStatus.InvalidArgument, anchor?.Id, null, "not a handle from this service");
            }
            return EraseByUuidAsync(h.Id);
        }

        public Task<AnchorResult> EraseByUuidAsync(string uuid)
        {
            EraseCount++;
            if (string.IsNullOrEmpty(uuid) || !Guid.TryParse(uuid, out _))
                return Done(AnchorStatus.InvalidArgument, uuid, null, "not a UUID");
            if (ForceErase is AnchorStatus forced && forced != AnchorStatus.Success)
                return Done(forced, uuid, null, "forced");

            _stored.Remove(uuid);
            if (_live.TryGetValue(uuid, out FakeHandle h)) { h.Kill(); _live.Remove(uuid); }
            return Done(AnchorStatus.Success, uuid, null, null);
        }

        /// <summary>Re-base tracking space: every live anchor moves in Unity
        /// world space, the physical piano does not.</summary>
        public void SimulateRecenter(Pose physicalToWorld) => TrackingOffset = physicalToWorld;

        /// <summary>The headset's map moves one anchor and not the others.</summary>
        public void SimulateAnchorDrift(string uuid, Vector3 physicalDelta)
        {
            if (_stored.TryGetValue(uuid, out Pose stored))
                _stored[uuid] = new Pose(stored.position + physicalDelta, stored.rotation);
            if (_live.TryGetValue(uuid, out FakeHandle h))
                h.Physical = new Pose(h.Physical.position + physicalDelta, h.Physical.rotation);
        }

        /// <summary>App quit: live handles die, saved anchors stay.</summary>
        public void EndSession()
        {
            foreach (FakeHandle h in _live.Values) h.Kill();
            _live.Clear();
        }

        private Pose ToPhysical(Pose world)
        {
            Quaternion inv = Quaternion.Inverse(TrackingOffset.rotation);
            return new Pose(inv * (world.position - TrackingOffset.position), inv * world.rotation);
        }

        private Pose ToWorld(Pose physical) =>
            new Pose(TrackingOffset.position + TrackingOffset.rotation * physical.position,
                     TrackingOffset.rotation * physical.rotation);

        private static Task<AnchorResult> Done(AnchorStatus status, string uuid, IPianoAnchorHandle handle,
                                               string detail, long localizeMs = -1) =>
            Task.FromResult(new AnchorResult(status, uuid, handle, detail, 0, localizeMs));

        private sealed class FakeHandle : IPianoAnchorHandle
        {
            public readonly EditorPianoAnchorService Owner;
            public Pose Physical;
            private bool _alive = true;

            public FakeHandle(EditorPianoAnchorService owner, string id, Pose physical)
            {
                Owner = owner; Id = id; Physical = physical;
            }

            public string Id { get; }
            public Pose Pose => _alive ? Owner.ToWorld(Physical) : Pose.identity;
            public bool IsTracked => _alive;
            public bool IsAlive => _alive;
            public void Kill() => _alive = false;
        }
    }
}
