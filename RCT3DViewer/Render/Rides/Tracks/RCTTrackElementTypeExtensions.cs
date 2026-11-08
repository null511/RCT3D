using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks;

internal static class RCTTrackElementTypeExtensions
{
    extension(RCTTrackElementType elementType)
    {
        public bool IsStationPlatform() => elementType is
            RCTTrackElementType.BeginStation or RCTTrackElementType.MiddleStation or RCTTrackElementType.EndStation;

        public bool IsLoop() => elementType is
            RCTTrackElementType.HalfLoopDown or RCTTrackElementType.HalfLoopUp or
            RCTTrackElementType.LeftVerticalLoop or RCTTrackElementType.RightVerticalLoop or
            RCTTrackElementType.LeftMediumHalfLoopDown or RCTTrackElementType.RightMediumHalfLoopDown or
            RCTTrackElementType.LeftMediumHalfLoopUp or RCTTrackElementType.RightMediumHalfLoopUp or
            RCTTrackElementType.LeftLargeHalfLoopDown or RCTTrackElementType.RightLargeHalfLoopDown or
            RCTTrackElementType.LeftLargeHalfLoopUp or RCTTrackElementType.RightLargeHalfLoopUp;

        public bool IsEighthTurn() => elementType is
            RCTTrackElementType.LeftEighthToDiag or RCTTrackElementType.RightEighthToDiag
            or RCTTrackElementType.LeftEighthToOrthogonal or RCTTrackElementType.RightEighthToOrthogonal
            or RCTTrackElementType.LeftEighthBankToDiag or RCTTrackElementType.RightEighthBankToDiag
            or RCTTrackElementType.LeftEighthBankToOrthogonal or RCTTrackElementType.RightEighthBankToOrthogonal;

        public bool IsDiagonal() => elementType is
            RCTTrackElementType.DiagFlat or RCTTrackElementType.DiagUp25 or RCTTrackElementType.DiagDown25
            or RCTTrackElementType.DiagFlatToUp25 or RCTTrackElementType.DiagUp25ToFlat
            or RCTTrackElementType.DiagFlatToDown25 or RCTTrackElementType.DiagDown25ToFlat;

        // TODO: needs many more parts
        public bool HasIncline() => elementType is
            RCTTrackElementType.FlatToUp25 or RCTTrackElementType.Up25ToFlat or
            RCTTrackElementType.FlatToDown25 or RCTTrackElementType.Down25ToFlat or
            RCTTrackElementType.Up25 or RCTTrackElementType.Down25;
    }

    //public static bool IsLift(this RCTTrackElementType elementType) => elementType is
    //    RCTTrackElementType.flattoup25
}
