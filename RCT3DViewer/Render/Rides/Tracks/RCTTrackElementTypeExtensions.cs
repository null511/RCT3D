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

        public bool HasIncline() => elementType is
            RCTTrackElementType.FlatToUp25 or RCTTrackElementType.Up25ToFlat or
            RCTTrackElementType.FlatToDown25 or RCTTrackElementType.Down25ToFlat or
            RCTTrackElementType.Up25 or RCTTrackElementType.Down25 or
            RCTTrackElementType.Up25ToUp60 or RCTTrackElementType.Up60ToUp25 or
            RCTTrackElementType.Up60 or RCTTrackElementType.Down60 or
            RCTTrackElementType.PoweredLift;

        public bool IsCovered() => elementType is
            RCTTrackElementType.FlatCovered or
            RCTTrackElementType.FlatToUp25Covered or
            RCTTrackElementType.FlatToDown25Covered or
            RCTTrackElementType.LeftQuarterTurn5TilesCovered or
            RCTTrackElementType.RightQuarterTurn5TilesCovered or
            RCTTrackElementType.LeftQuarterTurn3TilesCovered or
            RCTTrackElementType.RightQuarterTurn3TilesCovered or
            RCTTrackElementType.SBendLeftCovered or
            RCTTrackElementType.SBendRightCovered or
            RCTTrackElementType.Up25Covered or
            RCTTrackElementType.Up25ToFlatCovered or
            RCTTrackElementType.Up25ToUp60Covered or
            RCTTrackElementType.Up60Covered or
            RCTTrackElementType.Up60ToUp25Covered or
            RCTTrackElementType.Down25Covered or
            RCTTrackElementType.Down60Covered or
            RCTTrackElementType.Down25ToDown60Covered or
            RCTTrackElementType.Down60ToDown25Covered or
            RCTTrackElementType.Down25ToFlatCovered;
    }

    //public static bool IsLift(this RCTTrackElementType elementType) => elementType is
    //    RCTTrackElementType.flattoup25
}
