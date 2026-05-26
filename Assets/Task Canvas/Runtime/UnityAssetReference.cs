using System;

namespace TaskCanvas
{
    [Serializable]
    public class UnityAssetReference
    {
        public string id;
        public string assetGUID;
        public string assetPath;
        public string assetName;
        public string assetTypeName;
        public bool isSceneObject;
        public string scenePath;
        public string objectPath;

        public UnityAssetReference()
        {
            id = Guid.NewGuid().ToString();
        }
    }
}
