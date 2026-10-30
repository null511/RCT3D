namespace RCT3D.Render.Scenery;

internal sealed class TreeSceneryAssetLibrary
{
    private readonly string modelPath;

    private TreeSceneryAssetLibrary(string modelPath)
    {
        this.modelPath = modelPath;
    }

    public static TreeSceneryAssetLibrary Load(string modelDirectory)
    {
        var modelPath = Path.Combine(modelDirectory, "tree_01.glb");
        return new TreeSceneryAssetLibrary(modelPath);
    }

    public ImportedModelData LoadTreeModel() => new SharpGltfModelLoader().Load(modelPath);
}
