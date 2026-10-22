using System.Runtime.InteropServices;

namespace RCT3D.Interop;

public enum RCTRideType : ushort
{
    MiniatureRailway = 5,
    Monorail = 6,
    ClassicMiniRollerCoaster = 95
}

public enum RCTTrackElementType : ushort
{
    Flat = 0,
    EndStation = 1,
    BeginStation = 2,
    MiddleStation = 3,
    Up25 = 4,
    Up60 = 5,
    FlatToUp25 = 6,
    Up25ToFlat = 9,
    Down25 = 10,
    Down60 = 11,
    FlatToDown25 = 12,
    Down25ToDown60 = 13,
    Down60ToDown25 = 14,
    Down25ToFlat = 15,
    LeftQuarterTurn5Tiles = 16,
    RightQuarterTurn5Tiles = 17,
    FlatToLeftBank = 18,
    FlatToRightBank = 19,
    LeftBankToFlat = 20,
    RightBankToFlat = 21,
    BankedLeftQuarterTurn5Tiles = 22,
    BankedRightQuarterTurn5Tiles = 23,
    LeftBankToUp25 = 24,
    RightBankToUp25 = 25,
    Up25ToLeftBank = 26,
    Up25ToRightBank = 27,
    LeftBankToDown25 = 28,
    RightBankToDown25 = 29,
    Down25ToLeftBank = 30,
    Down25ToRightBank = 31,
    LeftBank = 32,
    RightBank = 33,
    LeftQuarterTurn5TilesUp25 = 34,
    RightQuarterTurn5TilesUp25 = 35,
    LeftQuarterTurn5TilesDown25 = 36,
    RightQuarterTurn5TilesDown25 = 37,
    SBendLeft = 38,
    SBendRight = 39,
    LeftHalfBankedHelixUpSmall = 87,
    RightHalfBankedHelixUpSmall = 88,
    LeftHalfBankedHelixDownSmall = 89,
    RightHalfBankedHelixDownSmall = 90,
    LeftHalfBankedHelixUpLarge = 91,
    RightHalfBankedHelixUpLarge = 92,
    LeftHalfBankedHelixDownLarge = 93,
    RightHalfBankedHelixDownLarge = 94,
    Brakes = 99,
    Booster = 100,
    LeftQuarterTurn3Tiles = 42,
    RightQuarterTurn3Tiles = 43,
    LeftBankedQuarterTurn3Tiles = 44,
    RightBankedQuarterTurn3Tiles = 45,
    LeftQuarterTurn3TilesUp25 = 46,
    RightQuarterTurn3TilesUp25 = 47,
    LeftQuarterTurn3TilesDown25 = 48,
    RightQuarterTurn3TilesDown25 = 49,
    LeftQuarterTurn1Tile = 50,
    RightQuarterTurn1Tile = 51,
    OnRidePhoto = 114,
    Down25Brakes = 339,
    BlockBrakes = 216,
    FlatToUp60 = 62,
    Up60ToFlat = 63,
    FlatToDown60 = 64,
    Down60ToFlat = 65,
    Up25ToUp60 = 7,
    Up60ToUp25 = 8,
    LeftEighthToDiagUp25 = 293,
    RightEighthToDiagUp25 = 294,
    LeftEighthToDiagDown25 = 295,
    RightEighthToDiagDown25 = 296,
    LeftEighthToOrthogonalUp25 = 297,
    RightEighthToOrthogonalUp25 = 298,
    LeftEighthToOrthogonalDown25 = 299,
    RightEighthToOrthogonalDown25 = 300,
    LeftEighthBankToDiagUp25 = 329,
    RightEighthBankToDiagUp25 = 330,
    LeftEighthBankToDiagDown25 = 331,
    RightEighthBankToDiagDown25 = 332,
    LeftEighthBankToOrthogonalUp25 = 333,
    RightEighthBankToOrthogonalUp25 = 334,
    LeftEighthBankToOrthogonalDown25 = 335,
    RightEighthBankToOrthogonalDown25 = 336,
    DiagUp25 = 142,
    DiagUp60 = 143,
    DiagFlatToUp25 = 144,
    DiagUp25ToUp60 = 145,
    DiagUp60ToUp25 = 146,
    DiagUp25ToFlat = 147,
    DiagDown25 = 148,
    DiagDown60 = 149,
    DiagFlatToDown25 = 150,
    DiagDown25ToDown60 = 151,
    DiagDown60ToDown25 = 152,
    DiagDown25ToFlat = 153,
    DiagFlatToUp60 = 154,
    DiagUp60ToFlat = 155,
    DiagFlatToDown60 = 156,
    DiagDown60ToFlat = 157,
    DiagBrakes = 337,
    DiagBlockBrakes = 338,
    DiagBooster = 340,
    LeftCurvedLiftHill = 209,
    RightCurvedLiftHill = 210,
    LeftEighthToDiag = 133,
    RightEighthToDiag = 134,
    LeftEighthToOrthogonal = 135,
    RightEighthToOrthogonal = 136,
    LeftEighthBankToDiag = 137,
    RightEighthBankToDiag = 138,
    LeftEighthBankToOrthogonal = 139,
    RightEighthBankToOrthogonal = 140,
    DiagFlat = 141,
    LeftQuarterTurn5TilesCovered = 81,
    RightQuarterTurn5TilesCovered = 82,
    SBendLeftCovered = 83,
    SBendRightCovered = 84,
    LeftQuarterTurn3TilesCovered = 85,
    RightQuarterTurn3TilesCovered = 86,
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTTrackElementData
{
    public int TileX;
    public int TileY;
    public RCTTrackElementType TrackType;
    public byte Sequence;
    public byte Direction;
    public byte BaseHeight;
    public byte MainPaletteIndex;
    public byte AdditionalPaletteIndex;
    public byte SequenceCount;
    public sbyte SequenceOffsetX;
    public sbyte SequenceOffsetY;
    public sbyte SequenceOffsetZ;
    public sbyte RotationBegin;
    public sbyte RotationEnd;
    public sbyte SplineZBegin;
    public sbyte SplineZEnd;
    public ushort RideId;
    public RCTRideType RideType;
    public sbyte RollBegin;
    public sbyte RollEnd;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTPaletteEntry
{
    public byte Red;
    public byte Green;
    public byte Blue;
    public byte Alpha;
}
