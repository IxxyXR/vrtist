﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.XR;

namespace VRtist
{
    public class AssetBankItem : ListItemContent
    {
        public int uid;               // uid
        public string assetName;      // item name or filename
        public HashSet<string> tags = new HashSet<string>();  // list of tags for filtering
        public GameObject original;   // original object (loaded from disk or Unity.Resources)
        public GameObject thumbnail;  // may be an image or 3D thumbnail
        public bool builtin = false;
        public Func<AssetBankItem, Task<GameObject>> importFunction = null;
        public bool skipInstantiation = false;
        public bool imported = false;

        public UIDynamicListItem uiItem;

        public void AddTags(string path)
        {
            path = path.Replace("\\", "/");
            foreach (var tag in path.Split(new char[] { '/', '-', '_', ' ' }))
            {
                tags.Add(tag.ToLower());
            }
        }
    }

    public class AssetBank : SelectorBase
    {
        private const string ASSET_BANK_NAME = "__VRtist_Asset_Bank__";

        [Header("Parameters")]
        private bool useDefaultInstantiationScale = false;

        private Dictionary<int, AssetBankItem> items = new Dictionary<int, AssetBankItem>();   // uid -> asset bank item
        private HashSet<string> tags = new HashSet<string>();
        private GameObject bank;  // contains all prefabs from the asset bank
        private int selectedItem = -1;
        private bool loadingAsset = false;

        private TaskCompletionSource<GameObject> blenderImportTask = null;
        private string requestedBlenderImportName;

        private UIDynamicList uiList;
        private UILabel filterLabel;

        void Start()
        {
            Init();

            uiList = panel.GetComponentInChildren<UIDynamicList>();
            uiList.focusItemOnAdd = false;

            filterLabel = panel.Find("ListPanel/FilterLabel").GetComponent<UILabel>();

            // Create our storage for loaded objects
            bank = new GameObject(ASSET_BANK_NAME);
            bank.SetActive(false);

            // Add our predifined objects
            AddBuiltinAssets();

            // Add user defined objects
            StartCoroutine(ScanDirectory(GlobalState.Settings.assetBankDirectory, () =>
            {
                // Add Blender asset bank assets
                GlobalState.blenderBankImportObjectEvent.AddListener(OnBlenderBankObjectImported);
                GlobalState.blenderBankListEvent.AddListener(OnBlenderBank);
                BlenderBankInfo info = new BlenderBankInfo { action = BlenderBankAction.ListRequest };
                MixerClient.Instance.SendBlenderBank(info);
            }));
        }

        public void OnEditFilter()
        {
            ToolsUIManager.Instance.OpenKeyboard(OnValidateFilter, panel, uiList.GetFilter());
        }

        private void OnValidateFilter(string value)
        {
            filterLabel.Text = value;
            uiList.OnFilterList(value);
        }

        public void OnClearFilter()
        {
            filterLabel.Text = "";
            uiList.OnFilterList(null);
        }

        public void ScanAssetBank()
        {
            // Remove all user assets
            foreach (var item in items.Values)
            {
                if (!item.builtin)
                {
                    if (item.original) { Destroy(item.original); }
                    if (item.thumbnail) { Destroy(item.thumbnail); }
                    items.Remove(item.uid);
                }
            }

            // Scan user directory
            StartCoroutine(ScanDirectory(GlobalState.Settings.assetBankDirectory, () => uiList.OnFirstPage()));

            // Rebuild tags list
            tags.Clear();
            foreach (var item in items.Values)
            {
                foreach (var tag in item.tags)
                {
                    tags.Add(tag);
                }
            }
        }

        private void AddBuiltinAssets()
        {
            AddBuiltinAsset("Empty", "Axis Locator", "Prefabs/UI/Axis Locator Item", "Prefabs/Primitives/Axis_locator");

            // TODO? parse them from Resources folder at editor time then create a db of those resources (scriptableObject)
            // available at runtime to finally parse that db
            AddBuiltinAsset("Primitive", "Cube", "Prefabs/UI/PRIMITIVES/CUBE_UI", "Prefabs/Primitives/PRIMITIVES/CUBE_PRIM");
            AddBuiltinAsset("Primitive", "Sphere", "Prefabs/UI/PRIMITIVES/SPHERE_UI", "Prefabs/Primitives/PRIMITIVES/SPHERE_PRIM");
            AddBuiltinAsset("Primitive", "Cylinder", "Prefabs/UI/PRIMITIVES/CYLINDER_UI", "Prefabs/Primitives/PRIMITIVES/CYLINDER_PRIM");
            AddBuiltinAsset("Primitive", "Cone", "Prefabs/UI/PRIMITIVES/CONE_UI", "Prefabs/Primitives/PRIMITIVES/CONE_PRIM");
            AddBuiltinAsset("Primitive", "Torus", "Prefabs/UI/PRIMITIVES/TORUS_UI", "Prefabs/Primitives/PRIMITIVES/TORUS_PRIM");
            AddBuiltinAsset("Primitive", "Plane", "Prefabs/UI/PRIMITIVES/PLANE_UI", "Prefabs/Primitives/PRIMITIVES/PLANE_PRIM");
            AddBuiltinAsset("Primitive", "Prism", "Prefabs/UI/PRIMITIVES/PRISM_UI", "Prefabs/Primitives/PRIMITIVES/PRISM_PRIM");
            AddBuiltinAsset("Primitive", "Pente", "Prefabs/UI/PRIMITIVES/PENTE_963_UI", "Prefabs/Primitives/PRIMITIVES/PENTE_963_PRIM");
            AddBuiltinAsset("Primitive", "Window", "Prefabs/UI/PRIMITIVES/WINDOW_UI", "Prefabs/Primitives/PRIMITIVES/WINDOW_PRIM");
            AddBuiltinAsset("Primitive", "Arch", "Prefabs/UI/PRIMITIVES/ARCH_QUARTER_UI", "Prefabs/Primitives/PRIMITIVES/ARCH_QUARTER_PRIM");
            AddBuiltinAsset("Primitive", "Stairs", "Prefabs/UI/PRIMITIVES/STRAIGHT_STAIRS_UI", "Prefabs/Primitives/PRIMITIVES/STRAIGHT_STAIRS_PRIM");
            AddBuiltinAsset("Primitive", "Circle Stairs", "Prefabs/UI/PRIMITIVES/CIRCLE_SQUARE_STAIRS_UI", "Prefabs/Primitives/PRIMITIVES/CIRCLE_SQUARE_STAIRS_PRIM");

            AddBuiltinAsset("Character", "Rabbid Gen Rest Neutral", "Prefabs/UI/CHARACTERS/UI_Rabbid_Gen_Rest_Neutral", "Prefabs/Primitives/CHARACTERS/Rabbid_Gen_Rest_Neutral");
            AddBuiltinAsset("Character", "Rabbid Gen Rest Bwaa", "Prefabs/UI/CHARACTERS/UI_Rabbid_Gen_Rest_Bwaa", "Prefabs/Primitives/CHARACTERS/Rabbid_Gen_Rest_Bwaa");
            AddBuiltinAsset("Character", "Rabbid Gen Rest Smile", "Prefabs/UI/CHARACTERS/UI_Rabbid_Gen_Rest_Smile", "Prefabs/Primitives/CHARACTERS/Rabbid_Gen_Rest_Smile");

            AddBuiltinAsset("Vegetation", "Aloe Vera", "Prefabs/UI/VEGETATION/UI_aloevera", "Prefabs/Primitives/VEGETATION/aloevera");
            AddBuiltinAsset("Vegetation", "Big Tree", "Prefabs/UI/VEGETATION/UI_big_tree", "Prefabs/Primitives/VEGETATION/big_tree");
            AddBuiltinAsset("Vegetation", "Small Tree", "Prefabs/UI/VEGETATION/UI_small_tree", "Prefabs/Primitives/VEGETATION/small_tree");
            AddBuiltinAsset("Vegetation", "Big Big Tree", "Prefabs/UI/VEGETATION/UI_big_big_tree", "Prefabs/Primitives/VEGETATION/big_big_tree");
            AddBuiltinAsset("Vegetation", "Banana Tree Hard", "Prefabs/UI/VEGETATION/UI_banana_tree_hard", "Prefabs/Primitives/VEGETATION/banana_tree_hard");
            AddBuiltinAsset("Vegetation", "Coconut Tree Hard", "Prefabs/UI/VEGETATION/UI_coconut_tree_hard", "Prefabs/Primitives/VEGETATION/coconut_tree_hard");
            AddBuiltinAsset("Vegetation", "Green Grass", "Prefabs/UI/VEGETATION/UI_green_grass", "Prefabs/Primitives/VEGETATION/green_grass");
            AddBuiltinAsset("Vegetation", "Yellow Grass", "Prefabs/UI/VEGETATION/UI_yellow_grass", "Prefabs/Primitives/VEGETATION/yellow_grass");
            AddBuiltinAsset("Vegetation", "Mushroom Amanita", "Prefabs/UI/VEGETATION/UI_mushroom_amanita", "Prefabs/Primitives/VEGETATION/mushroom_amanita");
            AddBuiltinAsset("Vegetation", "Mushroom Morel", "Prefabs/UI/VEGETATION/UI_mushroom_morel", "Prefabs/Primitives/VEGETATION/mushroom_morel");
            AddBuiltinAsset("Vegetation", "Wood A", "Prefabs/UI/VEGETATION/UI_wood_A", "Prefabs/Primitives/VEGETATION/wood_A");
            AddBuiltinAsset("Vegetation", "Wood B", "Prefabs/UI/VEGETATION/UI_wood_B", "Prefabs/Primitives/VEGETATION/wood_B");
            AddBuiltinAsset("Vegetation", "Wood C", "Prefabs/UI/VEGETATION/UI_wood_C", "Prefabs/Primitives/VEGETATION/wood_C");
            AddBuiltinAsset("Vegetation", "Log Wood", "Prefabs/UI/VEGETATION/UI_log_wood", "Prefabs/Primitives/VEGETATION/log_wood");

            AddBuiltinAsset("Rock", "Rock A", "Prefabs/UI/ROCKS/UI_rocks_round_A", "Prefabs/Primitives/ROCKS/rocks_round_A");
            AddBuiltinAsset("Rock", "Rock B", "Prefabs/UI/ROCKS/UI_rocks_round_B", "Prefabs/Primitives/ROCKS/rocks_round_B");
            AddBuiltinAsset("Rock", "Rock C", "Prefabs/UI/ROCKS/UI_rocks_round_C", "Prefabs/Primitives/ROCKS/rocks_round_C");
            AddBuiltinAsset("Rock", "Rock D", "Prefabs/UI/ROCKS/UI_rocks_round_D", "Prefabs/Primitives/ROCKS/rocks_round_D");
            AddBuiltinAsset("Rock", "Rock E", "Prefabs/UI/ROCKS/UI_rocks_round_E", "Prefabs/Primitives/ROCKS/rocks_round_E");
            AddBuiltinAsset("Rock", "Rock F", "Prefabs/UI/ROCKS/UI_rocks_sharp_F", "Prefabs/Primitives/ROCKS/rocks_sharp_F");
            AddBuiltinAsset("Rock", "Rock G", "Prefabs/UI/ROCKS/UI_rocks_sharp_G", "Prefabs/Primitives/ROCKS/rocks_sharp_G");
            AddBuiltinAsset("Rock", "Rock J", "Prefabs/UI/ROCKS/UI_rocks_round_J", "Prefabs/Primitives/ROCKS/rocks_round_J");
            AddBuiltinAsset("Rock", "Rock K", "Prefabs/UI/ROCKS/UI_rocks_round_K", "Prefabs/Primitives/ROCKS/rocks_round_K");
            AddBuiltinAsset("Rock", "Asteroid", "Prefabs/UI/ROCKS/UI_asteroid", "Prefabs/Primitives/ROCKS/asteroid");

            AddBuiltinAsset("Furniture", "Small Crate", "Prefabs/UI/FURNITURE/UI_small_crate", "Prefabs/Primitives/FURNITURE/small_crate");
            AddBuiltinAsset("Furniture", "Big Crate", "Prefabs/UI/FURNITURE/UI_big_crate", "Prefabs/Primitives/FURNITURE/big_crate");
            AddBuiltinAsset("Furniture", "Cactus pot", "Prefabs/UI/FURNITURE/UI_cactus_pot", "Prefabs/Primitives/FURNITURE/cactus_pot");
            AddBuiltinAsset("Furniture", "Stepladder", "Prefabs/UI/FURNITURE/UI_stepladder", "Prefabs/Primitives/FURNITURE/stepladder");
            AddBuiltinAsset("Furniture", "Armchair", "Prefabs/UI/FURNITURE/UI_armchair", "Prefabs/Primitives/FURNITURE/armchair");
            AddBuiltinAsset("Furniture", "Fishing Chair", "Prefabs/UI/FURNITURE/UI_fishing_chair", "Prefabs/Primitives/FURNITURE/fishing_chair");
            AddBuiltinAsset("Furniture", "Fridge", "Prefabs/UI/FURNITURE/UI_fridge", "Prefabs/Primitives/FURNITURE/fridge");
            AddBuiltinAsset("Furniture", "TV", "Prefabs/UI/FURNITURE/UI_tv", "Prefabs/Primitives/FURNITURE/tv");
            AddBuiltinAsset("Furniture", "Washing Machine", "Prefabs/UI/FURNITURE/UI_washing_machine", "Prefabs/Primitives/FURNITURE/washing_machine");

            AddBuiltinAsset("Prop", "Barrel", "Prefabs/UI/JUNK/UI_barrel", "Prefabs/Primitives/JUNK/barrel");
            AddBuiltinAsset("Prop", "Barricade", "Prefabs/UI/JUNK/UI_barricade", "Prefabs/Primitives/JUNK/barricade");
            AddBuiltinAsset("Prop", "Bench", "Prefabs/UI/JUNK/UI_bench", "Prefabs/Primitives/JUNK/bench");
            AddBuiltinAsset("Prop", "Bottle", "Prefabs/UI/JUNK/UI_bottle", "Prefabs/Primitives/JUNK/bottle");
            AddBuiltinAsset("Prop", "Bucket", "Prefabs/UI/JUNK/UI_bucket", "Prefabs/Primitives/JUNK/bucket");
            AddBuiltinAsset("Prop", "Caddie", "Prefabs/UI/JUNK/UI_caddie", "Prefabs/Primitives/JUNK/caddie");
            AddBuiltinAsset("Prop", "Dumpster", "Prefabs/UI/JUNK/UI_dumpster", "Prefabs/Primitives/JUNK/dumpster");
            AddBuiltinAsset("Prop", "Fence 1", "Prefabs/UI/JUNK/UI_fence_1", "Prefabs/Primitives/JUNK/fence_1");
            AddBuiltinAsset("Prop", "Fence 2", "Prefabs/UI/JUNK/UI_fence_2", "Prefabs/Primitives/JUNK/fence_2");
            AddBuiltinAsset("Prop", "Hydrant", "Prefabs/UI/JUNK/UI_hydrant", "Prefabs/Primitives/JUNK/hydrant");
            AddBuiltinAsset("Prop", "Metalsheet", "Prefabs/UI/JUNK/UI_metalsheet", "Prefabs/Primitives/JUNK/metalsheet");
            AddBuiltinAsset("Prop", "Paint", "Prefabs/UI/JUNK/UI_paint", "Prefabs/Primitives/JUNK/paint");
            AddBuiltinAsset("Prop", "Plank", "Prefabs/UI/JUNK/UI_plank", "Prefabs/Primitives/JUNK/plank");
            AddBuiltinAsset("Prop", "Tire", "Prefabs/UI/JUNK/UI_tire", "Prefabs/Primitives/JUNK/tire");
            AddBuiltinAsset("Prop", "Tole", "Prefabs/UI/JUNK/UI_tole", "Prefabs/Primitives/JUNK/tole");
            AddBuiltinAsset("Prop", "Trunk Army", "Prefabs/UI/JUNK/UI_trunk_army", "Prefabs/Primitives/JUNK/trunk_army");
            AddBuiltinAsset("Prop", "Warn Cone", "Prefabs/UI/JUNK/UI_warn_cone", "Prefabs/Primitives/JUNK/warn_cone");

            AddBuiltinAsset("Vehicle", "Car", "Prefabs/UI/JUNK/UI_car", "Prefabs/Primitives/JUNK/car");
            
            //AddBuiltinAsset("Prop", "Box", "Prefabs/UI/JUNK/UI_Box", "Prefabs/Primitives/JUNK/BOX_PRIM");
            //AddBuiltinAsset("Prop", "Ticket", "Prefabs/UI/JUNK/Ticket", "Prefabs/Primitives/JUNK/TICKET_PRIM");
            //AddBuiltinAsset("Vehicle", "Submarine", "Prefabs/UI/JUNK/Submarine", "Prefabs/Primitives/JUNK/SUBMARINE_PRIM");
            //AddBuiltinAsset("Prop", "Trunk Army", "Prefabs/UI/JUNK/TrunkArmy", "Prefabs/Primitives/JUNK/TRUNK_ARMY_PRIM");
            //AddBuiltinAsset("Prop", "Warn Cone", "Prefabs/UI/JUNK/WarnCone", "Prefabs/Primitives/JUNK/WARN_CONE_PRIM");
            //AddBuiltinAsset("Prop", "Washing Machine", "Prefabs/UI/JUNK/WashingMachine", "Prefabs/Primitives/JUNK/WASHING_MACHINE_PRIM");

            //AddBuiltinAsset("Vehicle", "Submarine", "Prefabs/UI/JUNK/Submarine", "Prefabs/Primitives/JUNK/SUBMARINE_PRIM");
        }

        private IEnumerator ScanDirectory(string path, Action onEndScan = null)
        {
            if (Directory.Exists(path))
            {
                string[] directories = Directory.GetDirectories(path);
                foreach (var directory in directories)
                {
                    StartCoroutine(ScanDirectory(directory));
                }

                string[] filenames = Directory.GetFiles(path, "*.fbx");
                foreach (var filename in filenames)
                {
                    AddFileAsset(filename);
                    yield return null;
                }
            }

            if (null != onEndScan) { onEndScan(); }
        }

        private void AddBuiltinAsset(string tags, string name, string uiPath, string originalPath)
        {
            GameObject original = Instantiate(Resources.Load<GameObject>(originalPath));
            original.transform.parent = bank.transform;
            GameObject thumbnail = UIGrabber.Create3DThumbnail(Instantiate(Resources.Load<GameObject>(uiPath)), OnUIObjectEnter, OnUIObjectExit);
            AssetBankItem item = AddAsset(name, thumbnail, original, tags);
            item.builtin = true;
        }

        private void AddFileAsset(string filename)
        {
            string name = Path.GetFileNameWithoutExtension(filename).Replace('_', ' ');
            GameObject thumbnail = UIGrabber.CreateTextThumbnail(name, OnUIObjectEnter, OnUIObjectExit);
            string tags = filename.Substring(GlobalState.Settings.assetBankDirectory.Length);
            tags = tags.Substring(0, tags.LastIndexOf('.'));  // remove file extension
            AddAsset(filename, thumbnail, null, tags, importFunction: ImportObjectAsync);
        }

        public void OnBlenderBank(List<string> names, List<string> tags, List<string> thumbnails)
        {
            // Load only once the whole asset bank data base from Blender
            GlobalState.blenderBankListEvent.RemoveListener(OnBlenderBank);
            StartCoroutine(AddBlenderAssets(names, tags, thumbnails));
        }

        public IEnumerator AddBlenderAssets(List<string> names, List<string> tags, List<string> thumbnails)
        {
            for (int i = 0; i < names.Count; i++)
            {
                AddBlenderAsset(names[i], tags[i], thumbnails[i]);
                yield return null;
            }
        }

        private void AddBlenderAsset(string name, string tags, string thumbnailPath)
        {
            GameObject thumbnail = UIGrabber.CreateLazyImageThumbnail(thumbnailPath, OnUIObjectEnter, OnUIObjectExit);
            AddAsset(name, thumbnail, null, tags, importFunction: ImportBlenderAsset, skipInstantiation: true);
        }

        public AssetBankItem AddAsset(string name, GameObject thumbnail, GameObject original, string tags, Func<AssetBankItem, Task<GameObject>> importFunction = null, bool skipInstantiation = false)
        {
            UIGrabber uiGrabber = thumbnail.GetComponent<UIGrabber>();
            if (null == uiGrabber)
            {
                Debug.LogError("Thumbnail game object must have a UIGrabber component. Use the UIGrabber.CreateXXXThumbnail helper functions to create such a thumbnail");
                return null;
            }
            int uid = thumbnail.GetHashCode();
            GameObject root = new GameObject("AssetBankItem");
            root.layer = LayerMask.NameToLayer("CameraHidden");
            AssetBankItem item = root.AddComponent<AssetBankItem>();
            item.uid = uid;
            item.assetName = name;
            item.gameObject.name = name;
            item.thumbnail = thumbnail;
            item.original = original;
            item.thumbnail.transform.parent = root.transform;
            item.thumbnail.transform.localPosition += new Vector3(0, 0, -0.001f);
            item.builtin = false;
            item.importFunction = importFunction;
            item.skipInstantiation = skipInstantiation;
            item.AddTags(tags);
            foreach (var tag in item.tags)
            {
                this.tags.Add(tag);
            }
            item.uiItem = uiList.AddItem(item.transform);
            items.Add(uid, item);
            return item;
        }

        public override void OnUIObjectEnter(int uid)
        {
            if (!loadingAsset)
            {
                selectedItem = uid;
            }
        }

        public override void OnUIObjectExit(int uid)
        {
            if (!loadingAsset)
            {
                selectedItem = -1;
            }
        }

        public void OnUseDefaultScale(bool value)
        {
            useDefaultInstantiationScale = value;
        }

        public async Task OnInstantiateUIObject()
        {
            if (loadingAsset) { return; }
            if (selectedItem == -1) { return; }
            if (!items.TryGetValue(selectedItem, out AssetBankItem item)) { return; }
            if (!item.thumbnail.GetComponent<UIGrabber>().isValid) { return; }

            // If the original doesn't exist, load it
            if (null == item.original)
            {
                loadingAsset = true;
                GlobalState.Instance.messageBox.ShowMessage("Loading asset, please wait...");
                Selection.ClearSelection();
                item.original = await item.importFunction(item);
                item.imported = true;

                // For blender assets, we don't want to instantiate objects, we will receive them
                if (!item.skipInstantiation)
                {
                    InstantiateObject(item.original);
                }
                else
                {
                    item.original = null;
                }
                GlobalState.Instance.messageBox.SetVisible(false);
                loadingAsset = false;
            }
            else
            {
                if (!item.skipInstantiation)
                {
                    InstantiateObject(item.original);
                }
            }
            selectedItem = -1;
        }

        private Task<GameObject> ImportObjectAsync(AssetBankItem item)
        {
            return GlobalState.GeometryImporter.ImportObjectAsync(item.assetName, bank.transform);
        }

        private Task<GameObject> ImportBlenderAsset(AssetBankItem item)
        {
            requestedBlenderImportName = item.assetName;
            blenderImportTask = new TaskCompletionSource<GameObject>();
            BlenderBankInfo info = new BlenderBankInfo { action = BlenderBankAction.ImportRequest, name = item.assetName };
            MixerClient.Instance.SendBlenderBank(info);
            return blenderImportTask.Task;
        }

        // Used for blender bank to know if a blender asset has been imported
        private void OnBlenderBankObjectImported(string objectName, string niceName)
        {
            if (niceName == requestedBlenderImportName && null != blenderImportTask && !blenderImportTask.Task.IsCompleted)
            {
                GameObject instance = SyncData.nodes[objectName].instances[0].Item1;
                blenderImportTask.TrySetResult(instance);
                Selection.AddToSelection(instance);
            }
        }

        private void InstantiateObject(GameObject gobject)
        {
            if (!items.TryGetValue(selectedItem, out AssetBankItem item))
            {
                Debug.LogWarning($"Item {gobject.name} not found in Asset Bank (id: {selectedItem})");
                return;
            }

            // Create the prefab and instantiate it
            GameObject newObject;
            if (item.imported)
            {
                newObject = SyncData.InstantiateFullHierarchyPrefab(SyncData.CreateFullHierarchyPrefab(gobject, ASSET_BANK_NAME));
                ParametersController controller = newObject.AddComponent<ParametersController>();
                controller.isImported = true;
                controller.importPath = item.assetName;
            }
            else
            {
                newObject = SyncData.InstantiatePrefab(SyncData.CreateInstance(gobject, SyncData.prefab));

                // Name the mesh
                newObject.GetComponentInChildren<MeshFilter>().mesh.name = gobject.GetComponentInChildren<MeshFilter>().mesh.name;
            }

            // Get the position of the mouthpiece into matrix
            Matrix4x4 matrix = rightHanded.worldToLocalMatrix * mouthpiece.localToWorldMatrix;
            Maths.DecomposeMatrix(matrix, out Vector3 t, out _, out _);
            Quaternion toRightHandedRotation = Quaternion.Euler(new Vector3(90f, 0f, 0f));
            Vector3 scale = Vector3.one;
            if (useDefaultInstantiationScale)
            {
                // The object keeps its real size independently of the user scale
                // Nothing to do
            }
            else
            {
                // Set the object size to 20cm in the user space
                Bounds bounds = new Bounds();
                MeshFilter[] meshFilters = gobject.GetComponentsInChildren<MeshFilter>();
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    Mesh mesh = meshFilter.mesh;
                    bounds.Encapsulate(mesh.bounds);
                }
                scale *= (0.2f / bounds.size.magnitude) / GlobalState.WorldScale;  // 0.2: 20cm
            }
            SyncData.SetTransform(newObject.name, Matrix4x4.TRS(t, Quaternion.identity, scale) * Matrix4x4.Rotate(toRightHandedRotation));

            CommandGroup group = new CommandGroup("Instantiate Bank Object");
            try
            {
                ClearSelection();
                if (null == newObject.GetComponent<MeshFilter>())
                {
                    // Send transform
                    new CommandAddGameObject(newObject).Submit();
                }
                foreach (var subMeshFilter in newObject.GetComponentsInChildren<MeshFilter>())
                {
                    new CommandAddGameObject(subMeshFilter.gameObject).Submit();
                }
                AddToSelection(newObject);
                Selection.HoveredObject = newObject;
            }
            finally
            {
                group.Submit();
            }
        }

        protected override void DoUpdateGui()
        {
            bool gripped = false;
            VRInput.ButtonEvent(VRInput.primaryController, CommonUsages.gripButton, async () =>
            {
                await OnInstantiateUIObject();

                // Since OnInstantiateUIObject may take some time, check we are still gripped
                gripped = VRInput.GetValue(VRInput.primaryController, CommonUsages.gripButton);
                if (gripped) { OnStartGrip(); }
            }, () =>
            {
                // Only end grip if we were effectively gripped
                if (gripped) { OnEndGrip(); }
            });
        }
    }
}
