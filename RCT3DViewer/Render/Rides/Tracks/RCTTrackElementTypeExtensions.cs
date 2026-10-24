using RCT3D.Interop;

namespace RCT3D.Render.Rides.Tracks;

internal static class RCTTrackElementTypeExtensions
{
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
