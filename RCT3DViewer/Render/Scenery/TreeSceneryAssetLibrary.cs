namespace RCT3D.Render.Scenery;

internal sealed class TreeSceneryAssetLibrary
{
    public TreeSceneryModel TreeModel { get; }

    private TreeSceneryAssetLibrary(TreeSceneryModel treeModel)
    {
        TreeModel = treeModel;
    }

    public static TreeSceneryAssetLibrary Load(string modelDirectory)
    {
        var modelPath = Path.Combine(modelDirectory, "tree_01.glb");
        var model = new SharpGltfModelLoader().Load(modelPath);
        return new TreeSceneryAssetLibrary(new TreeSceneryModel(modelPath, model));
    }
}

internal sealed record TreeSceneryModel(string AssetPath, ImportedModelData Data);
