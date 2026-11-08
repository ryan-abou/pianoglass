using System;
using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>How sure the app is about which hand plays what.</summary>
    public enum HandsStatus
    {
        /// <summary>No way to tell, so the song is practiced as All notes.</summary>
        Unknown,
        /// <summary>The track names say so.</summary>
        Named,
        /// <summary>Two tracks with no usable names: the higher one is the right hand.</summary>
        Guessed,
    }

    /// <summary>What HandAssigner needs to know about one track that has notes.</summary>
    public readonly struct TrackHandInfo
    {
        public readonly string Name;
        public readonly double AveragePitch;

        public TrackHandInfo(string name, double averagePitch)
        {
            Name = name;
            AveragePitch = averagePitch;
        }
    }

    public sealed class HandAssignment
    {
        public readonly HandsStatus Status;
        /// <summary>One per track passed in, in the same order.</summary>
        public readonly IReadOnlyList<NoteHand> Hands;

        public HandAssignment(HandsStatus status, IReadOnlyList<NoteHand> hands)
        {
            Status = status;
            Hands = hands;
        }
    }

    /// <summary>
    /// The only place hands are decided (#9 makes it smarter later).
    ///
    ///   1. Track names decide when every note track gets a hand from its
    ///      name and both hands are there.
    ///   2. Two note tracks that names can't decide: the higher one on
    ///      average is the right hand, marked as guessed.
    ///   3. Anything else, including every one-track file: hands unknown. No
    ///      split at middle C, and a name-based hand is never mixed with a guess.
    /// </summary>
    public static class HandAssigner
    {
        /// <param name="noteTracks">Only the tracks with notes.</param>
        /// <param name="swap">The song's "swap hands" setting.</param>
        public static HandAssignment Assign(IReadOnlyList<TrackHandInfo> noteTracks, bool swap = false)
        {
            HandAssignment result = Decide(noteTracks);
            if (!swap || result.Status == HandsStatus.Unknown) return result;

            var swapped = new NoteHand[result.Hands.Count];
            for (int i = 0; i < swapped.Length; i++) swapped[i] = Swap(result.Hands[i]);
            return new HandAssignment(result.Status, swapped);
        }

        private static HandAssignment Decide(IReadOnlyList<TrackHandInfo> tracks)
        {
            int count = tracks.Count;
            var hands = new NoteHand[count];

            if (count >= 2 && TryUseNames(tracks, hands))
                return new HandAssignment(HandsStatus.Named, hands);

            if (count == 2)
            {
                bool firstIsHigher = tracks[0].AveragePitch >= tracks[1].AveragePitch;
                hands[0] = firstIsHigher ? NoteHand.Right : NoteHand.Left;
                hands[1] = firstIsHigher ? NoteHand.Left : NoteHand.Right;
                return new HandAssignment(HandsStatus.Guessed, hands);
            }

            Array.Clear(hands, 0, count);
            return new HandAssignment(HandsStatus.Unknown, hands);
        }

        private static bool TryUseNames(IReadOnlyList<TrackHandInfo> tracks, NoteHand[] hands)
        {
            bool anyLeft = false, anyRight = false;
            for (int i = 0; i < tracks.Count; i++)
            {
                hands[i] = HandFromName(tracks[i].Name);
                if (hands[i] == NoteHand.None) return false;
                anyLeft |= hands[i] == NoteHand.Left;
                anyRight |= hands[i] == NoteHand.Right;
            }
            return anyLeft && anyRight;
        }

        /// <summary>What a track's name says. None when it says nothing, or
        /// says both. Any capitals; LH and RH only as whole words, so
        /// "Rhythm" doesn't count.</summary>
        public static NoteHand HandFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return NoteHand.None;
            string lower = name.ToLowerInvariant();

            bool left = lower.Contains("left") || lower.Contains("bass") || HasWord(lower, "lh");
            bool right = lower.Contains("right") || lower.Contains("treble") || HasWord(lower, "rh");
            if (left == right) return NoteHand.None;
            return left ? NoteHand.Left : NoteHand.Right;
        }

        private static bool HasWord(string text, string word)
        {
            int at = text.IndexOf(word, StringComparison.Ordinal);
            while (at >= 0)
            {
                int after = at + word.Length;
                bool startsClean = at == 0 || !char.IsLetter(text[at - 1]);
                bool endsClean = after == text.Length || !char.IsLetter(text[after]);
                if (startsClean && endsClean) return true;
                at = text.IndexOf(word, at + 1, StringComparison.Ordinal);
            }
            return false;
        }

        /// <summary>Left and right flip. Shared stays shared, and None stays None.</summary>
        public static NoteHand Swap(NoteHand hand) =>
            hand == NoteHand.Left ? NoteHand.Right : hand == NoteHand.Right ? NoteHand.Left : hand;
    }
}
