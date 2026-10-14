using System.Threading.Tasks;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// What a spatial-anchor call came back with, without any Meta types in
    /// it. OvrPianoAnchorService maps the SDK's result codes onto these; the
    /// editor fake returns whichever one a test asks for.
    /// </summary>
    public enum AnchorStatus
    {
        Success,
        /// <summary>Load succeeded but the UUID came back empty: the anchor is
        /// gone (uninstall, spatial data cleared). Not an error to retry.</summary>
        NotFound,
        /// <summary>Loaded but did not localize in time. Retry; never erase.</summary>
        Timeout,
        /// <summary>The runtime refused or failed. Detail says why.</summary>
        Failed,
        /// <summary>The caller passed something unusable (empty UUID, a
        /// handle that no longer exists).</summary>
        InvalidArgument,
    }

    /// <summary>One anchor call's outcome, with timings for the device log.</summary>
    public sealed class AnchorResult
    {
        public AnchorStatus Status { get; }
        public string Uuid { get; }
        /// <summary>Set by create and load; null otherwise.</summary>
        public IPianoAnchorHandle Handle { get; }
        /// <summary>The SDK's own result code, verbatim, for the log.</summary>
        public string Detail { get; }
        public long ElapsedMs { get; }
        /// <summary>Load only: how long the localize step took, -1 if not reached.</summary>
        public long LocalizeMs { get; }

        public bool Ok => Status == AnchorStatus.Success;

        public AnchorResult(AnchorStatus status, string uuid, IPianoAnchorHandle handle,
                            string detail, long elapsedMs, long localizeMs = -1)
        {
            Status = status; Uuid = uuid; Handle = handle;
            Detail = detail; ElapsedMs = elapsedMs; LocalizeMs = localizeMs;
        }

        public override string ToString() =>
            $"status={Status} uuid={(string.IsNullOrEmpty(Uuid) ? "-" : Uuid)} " +
            $"elapsed={ElapsedMs}ms" + (LocalizeMs >= 0 ? $" localize={LocalizeMs}ms" : "") +
            (string.IsNullOrEmpty(Detail) ? "" : $" detail={Detail}");
    }

    /// <summary>A live anchor in this session. Its pose is re-read every
    /// frame by the runtime, so it moves in Unity world space whenever
    /// tracking space is re-based -- that is the whole point.</summary>
    public interface IPianoAnchorHandle
    {
        /// <summary>The persistent UUID, as a "D"-format string (JsonUtility
        /// cannot serialize System.Guid).</summary>
        string Id { get; }
        Pose Pose { get; }
        bool IsTracked { get; }
        /// <summary>False once the underlying anchor object is destroyed.</summary>
        bool IsAlive { get; }
    }

    /// <summary>
    /// The four operations the piano needs from a spatial-anchor runtime.
    /// No OVRTask, OVRSpatialAnchor or Guid-only API in the signature, so
    /// the editor and tests can fake it.
    ///
    /// Rules every implementation follows:
    ///   * Nothing here creates an anchor except CreateAsync.
    ///   * Nothing here erases an anchor except the two Erase methods, and
    ///     neither is ever called on a failure path.
    ///   * A load that comes back empty is NotFound, not Failed.
    /// </summary>
    public interface IPianoAnchorService
    {
        /// <summary>Create an anchor at this world pose and wait until the
        /// runtime has it. Not yet persisted -- call SaveAsync.</summary>
        Task<AnchorResult> CreateAsync(Vector3 position, Quaternion rotation);

        Task<AnchorResult> SaveAsync(IPianoAnchorHandle anchor);

        /// <summary>Load one persisted anchor by UUID, localize it (waiting
        /// up to <paramref name="localizeTimeoutSeconds"/>), and bind it to a
        /// live handle. Binding happens in the same frame the handle is
        /// created, so a load never creates a new anchor by accident.</summary>
        Task<AnchorResult> LoadAsync(string uuid, double localizeTimeoutSeconds);

        /// <summary>Erase a live anchor from storage and destroy its handle.</summary>
        Task<AnchorResult> EraseAsync(IPianoAnchorHandle anchor);

        /// <summary>Erase by UUID without loading it first.</summary>
        Task<AnchorResult> EraseByUuidAsync(string uuid);
    }
}
