using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks;

internal static class RCTTrackElementTypeExtensions
{
    public static bool IsStationPlatform(this RCTTrackElementType elementType) => elementType is
        RCTTrackElementType.BeginStation or RCTTrackElementType.MiddleStation or RCTTrackElementType.EndStation;

    //public static bool IsLift(this RCTTrackElementType elementType) => elementType is
    //    RCTTrackElementType.flattoup25

    public static bool IsLoop(this RCTTrackElementType trackType) => trackType is
        RCTTrackElementType.HalfLoopDown or RCTTrackElementType.HalfLoopUp or
        RCTTrackElementType.LeftVerticalLoop or RCTTrackElementType.RightVerticalLoop or
        RCTTrackElementType.LeftMediumHalfLoopDown or RCTTrackElementType.RightMediumHalfLoopDown or
        RCTTrackElementType.LeftMediumHalfLoopUp or RCTTrackElementType.RightMediumHalfLoopUp or
        RCTTrackElementType.LeftLargeHalfLoopDown or RCTTrackElementType.RightLargeHalfLoopDown or
        RCTTrackElementType.LeftLargeHalfLoopUp or RCTTrackElementType.RightLargeHalfLoopUp;

    public static bool IsEighthTurn(this RCTTrackElementType trackType) => trackType is
        RCTTrackElementType.LeftEighthToDiag or RCTTrackElementType.RightEighthToDiag
        or RCTTrackElementType.LeftEighthToOrthogonal or RCTTrackElementType.RightEighthToOrthogonal
        or RCTTrackElementType.LeftEighthBankToDiag or RCTTrackElementType.RightEighthBankToDiag
        or RCTTrackElementType.LeftEighthBankToOrthogonal or RCTTrackElementType.RightEighthBankToOrthogonal;

    public static bool IsDiagonal(this RCTTrackElementType trackType) => trackType is
        RCTTrackElementType.DiagFlat or RCTTrackElementType.DiagUp25 or RCTTrackElementType.DiagDown25
        or RCTTrackElementType.DiagFlatToUp25 or RCTTrackElementType.DiagUp25ToFlat
        or RCTTrackElementType.DiagFlatToDown25 or RCTTrackElementType.DiagDown25ToFlat;
}
