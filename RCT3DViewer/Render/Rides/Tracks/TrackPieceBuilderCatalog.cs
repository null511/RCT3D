using RCT3D.Interop.Models;

namespace RCT3D.Render.Rides.Tracks;

internal sealed class TrackPieceBuilderCatalog
{
    private readonly Dictionary<RCTTrackElementType, ITrackPieceMeshBuilder> builders = new();

    public static TrackPieceBuilderCatalog Shared { get; } = Create();

    public IEnumerable<ITrackPieceMeshBuilder> Builders => builders.Values;

    public bool TryResolve(RCTTrackElementType type, out ITrackPieceMeshBuilder builder) => builders.TryGetValue(type, out builder!);

    private static TrackPieceBuilderCatalog Create()
    {
        var catalog = new TrackPieceBuilderCatalog();

        catalog.RegisterMany(type => new StraightTrackPieceBuilder(type),
            RCTTrackElementType.BeginStation,
            RCTTrackElementType.BlockBrakes,
            RCTTrackElementType.Booster,
            RCTTrackElementType.Brakes,
            RCTTrackElementType.DiagBooster,
            RCTTrackElementType.Down25Brakes,
            RCTTrackElementType.EndStation,
            RCTTrackElementType.Flat,
            RCTTrackElementType.LeftBank,
            RCTTrackElementType.MiddleStation,
            RCTTrackElementType.OnRidePhoto,
            RCTTrackElementType.RightBank);

        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25, Pitch.Slope25, Pitch.Slope25));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60, Pitch.Slope60, Pitch.Slope60));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp25, Pitch.None, Pitch.Slope25));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToFlat, Pitch.Slope25, Pitch.None));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up25ToUp60, Pitch.Slope25, Pitch.Slope60));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToUp25, Pitch.Slope60, Pitch.Slope25));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25, -Pitch.Slope25, -Pitch.Slope25));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60, -Pitch.Slope60, -Pitch.Slope60));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown25, Pitch.None, -Pitch.Slope25));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToFlat, -Pitch.Slope25, Pitch.None));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down25ToDown60, -Pitch.Slope25, -Pitch.Slope60));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToDown25, -Pitch.Slope60, -Pitch.Slope25));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToUp60, Pitch.None, Pitch.Slope60));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlat, Pitch.Slope60, Pitch.None));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60, Pitch.None, -Pitch.Slope60));
        catalog.Register(new SlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlat, -Pitch.Slope60, Pitch.None));

        catalog.RegisterMany(type => new LongBaseSlopeTrackPieceBuilder(type, Pitch.None, Pitch.Slope60),
            RCTTrackElementType.FlatToUp60LongBase);
        catalog.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.Up60ToFlatLongBase, Pitch.Slope60, Pitch.None));
        catalog.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.FlatToDown60LongBase, Pitch.None, -Pitch.Slope60));
        catalog.Register(new LongBaseSlopeTrackPieceBuilder(RCTTrackElementType.Down60ToFlatLongBase, -Pitch.Slope60, Pitch.None));

        catalog.RegisterMany(type => new CurveTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn1Tile,
            RCTTrackElementType.LeftQuarterTurn3Tiles,
            RCTTrackElementType.LeftQuarterTurn3TilesCovered,
            RCTTrackElementType.LeftQuarterTurn3TilesDown25,
            RCTTrackElementType.LeftQuarterTurn3TilesUp25,
            RCTTrackElementType.LeftQuarterTurn5Tiles,
            RCTTrackElementType.LeftQuarterTurn5TilesCovered,
            RCTTrackElementType.LeftQuarterTurn5TilesDown25,
            RCTTrackElementType.LeftQuarterTurn5TilesUp25,
            RCTTrackElementType.RightQuarterTurn1Tile,
            RCTTrackElementType.RightQuarterTurn3Tiles,
            RCTTrackElementType.RightQuarterTurn3TilesCovered,
            RCTTrackElementType.RightQuarterTurn3TilesDown25,
            RCTTrackElementType.RightQuarterTurn3TilesUp25,
            RCTTrackElementType.RightQuarterTurn5Tiles,
            RCTTrackElementType.RightQuarterTurn5TilesCovered,
            RCTTrackElementType.RightQuarterTurn5TilesDown25,
            RCTTrackElementType.RightQuarterTurn5TilesUp25,
            RCTTrackElementType.SBendLeft,
            RCTTrackElementType.SBendLeftCovered,
            RCTTrackElementType.SBendRight,
            RCTTrackElementType.SBendRightCovered);

        catalog.RegisterMany(type => new BankedCurveTrackPieceBuilder(type),
            RCTTrackElementType.BankedLeftQuarterTurn5Tiles,
            RCTTrackElementType.BankedRightQuarterTurn5Tiles,
            RCTTrackElementType.FlatToLeftBank,
            RCTTrackElementType.FlatToRightBank,
            RCTTrackElementType.LeftBankedQuarterTurn3TileDown25,
            RCTTrackElementType.LeftBankedQuarterTurn3Tiles,
            RCTTrackElementType.LeftBankedQuarterTurn3TileUp25,
            RCTTrackElementType.LeftBankedQuarterTurn5TileDown25,
            RCTTrackElementType.LeftBankedQuarterTurn5TileUp25,
            RCTTrackElementType.LeftBankToFlat,
            RCTTrackElementType.LeftEighthBankToDiag,
            RCTTrackElementType.LeftEighthBankToDiagDown25,
            RCTTrackElementType.LeftEighthBankToDiagUp25,
            RCTTrackElementType.LeftEighthBankToOrthogonal,
            RCTTrackElementType.LeftEighthBankToOrthogonalDown25,
            RCTTrackElementType.LeftEighthBankToOrthogonalUp25,
            RCTTrackElementType.LeftEighthToDiag,
            RCTTrackElementType.LeftEighthToDiagDown25,
            RCTTrackElementType.LeftEighthToDiagUp25,
            RCTTrackElementType.LeftEighthToOrthogonal,
            RCTTrackElementType.LeftEighthToOrthogonalDown25,
            RCTTrackElementType.LeftEighthToOrthogonalUp25,
            RCTTrackElementType.RightBankedQuarterTurn3TileDown25,
            RCTTrackElementType.RightBankedQuarterTurn3Tiles,
            RCTTrackElementType.RightBankedQuarterTurn3TileUp25,
            RCTTrackElementType.RightBankedQuarterTurn5TileDown25,
            RCTTrackElementType.RightBankedQuarterTurn5TileUp25,
            RCTTrackElementType.RightBankToFlat,
            RCTTrackElementType.RightEighthBankToDiag,
            RCTTrackElementType.RightEighthBankToDiagDown25,
            RCTTrackElementType.RightEighthBankToDiagUp25,
            RCTTrackElementType.RightEighthBankToOrthogonal,
            RCTTrackElementType.RightEighthBankToOrthogonalDown25,
            RCTTrackElementType.RightEighthBankToOrthogonalUp25,
            RCTTrackElementType.RightEighthToDiag,
            RCTTrackElementType.RightEighthToDiagDown25,
            RCTTrackElementType.RightEighthToDiagUp25,
            RCTTrackElementType.RightEighthToOrthogonal,
            RCTTrackElementType.RightEighthToOrthogonalDown25,
            RCTTrackElementType.RightEighthToOrthogonalUp25,
            RCTTrackElementType.RightQuarterBankedHelixLargeUp);

        catalog.RegisterMany(type => new SteepQuarterTurnTrackPieceBuilder(type),
            RCTTrackElementType.LeftQuarterTurn1TileDown60,
            RCTTrackElementType.LeftQuarterTurn1TileUp60,
            RCTTrackElementType.RightQuarterTurn1TileDown60,
            RCTTrackElementType.RightQuarterTurn1TileUp60);

        catalog.RegisterMany(type => new HelixTrackPieceBuilder(type),
            RCTTrackElementType.LeftHalfBankedHelixDownLarge,
            RCTTrackElementType.LeftHalfBankedHelixDownSmall,
            RCTTrackElementType.LeftHalfBankedHelixUpLarge,
            RCTTrackElementType.LeftHalfBankedHelixUpSmall,
            RCTTrackElementType.RightHalfBankedHelixDownLarge,
            RCTTrackElementType.RightHalfBankedHelixDownSmall,
            RCTTrackElementType.RightHalfBankedHelixUpLarge,
            RCTTrackElementType.RightHalfBankedHelixUpSmall);

        catalog.Register(new DiagonalFlatTrackPieceBuilder());
        catalog.RegisterMany(type => new DiagonalTrackPieceBuilder(type),
            RCTTrackElementType.DiagBlockBrakes,
            RCTTrackElementType.DiagBrakes,
            RCTTrackElementType.DiagDown25,
            RCTTrackElementType.DiagDown25ToDown60,
            RCTTrackElementType.DiagDown25ToFlat,
            RCTTrackElementType.DiagDown25ToLeftBank,
            RCTTrackElementType.DiagDown25ToRightBank,
            RCTTrackElementType.DiagDown60,
            RCTTrackElementType.DiagDown60ToDown25,
            RCTTrackElementType.DiagDown60ToFlat,
            RCTTrackElementType.DiagDown60ToFlatLongBase,
            RCTTrackElementType.DiagFlatToDown25,
            RCTTrackElementType.DiagFlatToDown60,
            RCTTrackElementType.DiagFlatToDown60LongBase,
            RCTTrackElementType.DiagFlatToLeftBank,
            RCTTrackElementType.DiagFlatToRightBank,
            RCTTrackElementType.DiagFlatToUp25,
            RCTTrackElementType.DiagFlatToUp60,
            RCTTrackElementType.DiagFlatToUp60LongBase,
            RCTTrackElementType.DiagLeftBank,
            RCTTrackElementType.DiagLeftBankToDown25,
            RCTTrackElementType.DiagLeftBankToFlat,
            RCTTrackElementType.DiagLeftBankToUp25,
            RCTTrackElementType.DiagRightBank,
            RCTTrackElementType.DiagRightBankToDown25,
            RCTTrackElementType.DiagRightBankToFlat,
            RCTTrackElementType.DiagRightBankToUp25,
            RCTTrackElementType.DiagUp25,
            RCTTrackElementType.DiagUp25ToFlat,
            RCTTrackElementType.DiagUp25ToLeftBank,
            RCTTrackElementType.DiagUp25ToRightBank,
            RCTTrackElementType.DiagUp25ToUp60,
            RCTTrackElementType.DiagUp60,
            RCTTrackElementType.DiagUp60ToFlat,
            RCTTrackElementType.DiagUp60ToFlatLongBase,
            RCTTrackElementType.DiagUp60ToUp25);

        catalog.RegisterMany(type => new BankedSlopeTrackPieceBuilder(type),
            RCTTrackElementType.Down25ToLeftBank,
            RCTTrackElementType.Down25ToRightBank,
            RCTTrackElementType.LeftBankToDown25,
            RCTTrackElementType.LeftBankToUp25,
            RCTTrackElementType.RightBankToDown25,
            RCTTrackElementType.RightBankToUp25,
            RCTTrackElementType.Up25ToLeftBank,
            RCTTrackElementType.Up25ToRightBank);

        catalog.RegisterMany(type => new VerticalLoopTrackPieceBuilder(type),
            RCTTrackElementType.LeftVerticalLoop,
            RCTTrackElementType.RightVerticalLoop);

        catalog.RegisterMany(type => new HalfLoopTrackPieceBuilder(type),
            RCTTrackElementType.HalfLoopDown,
            RCTTrackElementType.HalfLoopUp);

        catalog.RegisterMany(type => new MediumHalfLoopTrackPieceBuilder(type),
            RCTTrackElementType.LeftMediumHalfLoopDown,
            RCTTrackElementType.LeftMediumHalfLoopUp,
            RCTTrackElementType.RightMediumHalfLoopDown,
            RCTTrackElementType.RightMediumHalfLoopUp);

        catalog.RegisterMany(type => new LargeHalfLoopTrackPieceBuilder(type),
            RCTTrackElementType.LeftLargeHalfLoopDown,
            RCTTrackElementType.LeftLargeHalfLoopUp,
            RCTTrackElementType.RightLargeHalfLoopDown,
            RCTTrackElementType.RightLargeHalfLoopUp);

        catalog.RegisterMany(type => new CorkscrewTrackPieceBuilder(type),
            RCTTrackElementType.LeftCorkscrewDown,
            RCTTrackElementType.LeftCorkscrewUp,
            RCTTrackElementType.RightCorkscrewDown,
            RCTTrackElementType.RightCorkscrewUp,
            RCTTrackElementType.LeftLargeCorkscrewDown,
            RCTTrackElementType.LeftLargeCorkscrewUp,
            RCTTrackElementType.RightLargeCorkscrewDown,
            RCTTrackElementType.RightLargeCorkscrewUp);

        catalog.RegisterMany(type => new TwistTrackPieceBuilder(type),
            RCTTrackElementType.LeftTwistDownToUp,
            RCTTrackElementType.RightTwistDownToUp,
            RCTTrackElementType.RightTwistUpToDown);

        return catalog;
    }

    private void Register(ITrackPieceMeshBuilder builder) => builders.Add(builder.PieceType, builder);

    private void RegisterMany<TBuilder>(Func<RCTTrackElementType, TBuilder> createBuilder, params RCTTrackElementType[] pieceTypes)
        where TBuilder : ITrackPieceMeshBuilder
    {
        foreach (var pieceType in pieceTypes)
            Register(createBuilder(pieceType));
    }
}
