using System.Runtime.InteropServices;

namespace RCT3D.Interop;

public enum RCTRideType : ushort
{
    MiniatureRailway = 5,
    Monorail = 6
}

public enum RCTTrackElementType : ushort
{
    Flat = 0,
    EndStation = 1,
    BeginStation = 2,
    MiddleStation = 3,
    Up25 = 4,
    FlatToUp25 = 6,
    Up25ToFlat = 9,
    Down25 = 10,
    FlatToDown25 = 12,
    Down25ToFlat = 15,
    LeftQuarterTurn5Tiles = 16,
    RightQuarterTurn5Tiles = 17,
    SBendLeft = 38,
    SBendRight = 39,
    LeftQuarterTurn3Tiles = 42,
    RightQuarterTurn3Tiles = 43,
    LeftQuarterTurn1Tile = 50,
    RightQuarterTurn1Tile = 51,
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
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RCTPaletteEntry
{
    public byte Red;
    public byte Green;
    public byte Blue;
    public byte Alpha;
}
