using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks;

internal static class TrackBasedSupportProgressMap
{
    private const float EighthTurnInsetProgress = 16.0f / 87.0f;

    private static readonly float[] defaultProgress = [0.5f];
    private static readonly float[] noProgress = [];
    private static readonly float[] firstEighthTurnProgress = [EighthTurnInsetProgress];
    private static readonly float[] lastEighthTurnProgress = [1.0f - EighthTurnInsetProgress];

    private static readonly IReadOnlyDictionary<RCTTrackElementType, Func<RCTTrackElementData, float[]>> mappings =
        new Dictionary<RCTTrackElementType, Func<RCTTrackElementData, float[]>> {
            [RCTTrackElementType.Flat] = GetStraightProgress,
            [RCTTrackElementType.DiagFlat] = GetStraightProgress,
            [RCTTrackElementType.Down25] = GetStraightProgress,
            [RCTTrackElementType.Up25] = GetStraightProgress,
            [RCTTrackElementType.SBendLeft] = GetSBendProgress,
            [RCTTrackElementType.SBendRight] = GetSBendProgress,
            [RCTTrackElementType.SBendLeftCovered] = GetSBendProgress,
            [RCTTrackElementType.SBendRightCovered] = GetSBendProgress,
            [RCTTrackElementType.LeftEighthToDiag] = GetEighthTurnProgress,
            [RCTTrackElementType.RightEighthToDiag] = GetEighthTurnProgress,
            [RCTTrackElementType.LeftEighthToOrthogonal] = GetEighthTurnProgress,
            [RCTTrackElementType.RightEighthToOrthogonal] = GetEighthTurnProgress,
            [RCTTrackElementType.LeftEighthBankToDiag] = GetEighthTurnProgress,
            [RCTTrackElementType.RightEighthBankToDiag] = GetEighthTurnProgress,
            [RCTTrackElementType.LeftEighthBankToOrthogonal] = GetEighthTurnProgress,
            [RCTTrackElementType.RightEighthBankToOrthogonal] = GetEighthTurnProgress,
        };

    public static float[] GetProgresses(in RCTTrackElementData track) =>
        mappings.TryGetValue(track.Part, out var getProgresses)
            ? getProgresses(track)
            : defaultProgress;

    private static float[] GetStraightProgress(RCTTrackElementData track) =>
        track.Sequence % 2 == 0 ? defaultProgress : noProgress;

    private static float[] GetSBendProgress(RCTTrackElementData track) =>
        [(track.Sequence + 0.5f) / track.SequenceCount];

    private static float[] GetEighthTurnProgress(RCTTrackElementData track)
    {
        if (track.Sequence == 0)
            return firstEighthTurnProgress;

        if (track.Sequence == track.SequenceCount - 1)
            return lastEighthTurnProgress;

        return noProgress;
    }
}
