using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System;
using System.Reflection;

public class UnityToolCenter : EditorWindow 
{
    private static readonly string moduleOrDllName = "UnityToolCenter";
    private static readonly string ivyFileName = "ivy.xml";
	private static readonly string configFileName = "config.ini";
    public static string ExtensionsDir
    {
        get
        {
            return Path.Combine(EditorApplication.applicationContentsPath,
                "UnityExtensions" + Path.DirectorySeparatorChar + "Unity" + Path.DirectorySeparatorChar);
        }
    }
	public static string ConfigPathDir
	{
		get
		{
			return Path.Combine(Path.Combine(ExtensionsDir, moduleOrDllName), configFileName);
		}
	}

	private static List<string> packagePathList = new List<string>();
    private static Dictionary<string,bool> packageSelectDic = new Dictionary<string,bool>();
	private static UnityToolCenter window = null;
	private Vector2 unityPackageScrollViewPos = Vector2.zero;

    [MenuItem("Tools/UnityToolCenter")]
	public static void OpenUnityToolCenter()
	{
        string moduleDir = Path.Combine(ExtensionsDir, moduleOrDllName);
        packagePathList.Clear();
        if (Directory.Exists(moduleDir) && File.Exists(ConfigPathDir))
        {
            string config = string.Empty;
            using(FileStream fs = new FileStream(ConfigPathDir, FileMode.Open))
			{
                //// "GB2312"用于显示中文字符，写其他的，中文会显示乱码
                StreamReader reader = new StreamReader(fs, UnicodeEncoding.GetEncoding("GB2312"));

                //// 一行一行读取
                while ((config = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrEmpty(config))
                    {
                        GetFiles(new DirectoryInfo(config), "*.unitypackage", ref packagePathList);
                    }
                }
			}
        }
        packageSelectDic.Clear();
        for (int i = 0; i < packagePathList.Count; i++)
        {
            packageSelectDic.Add(packagePathList[i],false);
        }
        if (window == null)
            window = EditorWindow.GetWindow(typeof(UnityToolCenter)) as UnityToolCenter;
        window.titleContent = new GUIContent("UnityToolCenter");
        window.Show();
	}

	private void OnGUI() 
	{
		if(GUILayout.Button("Reload"))
		{
			
		}
		unityPackageScrollViewPos = EditorGUILayout.BeginScrollView(unityPackageScrollViewPos);
		if(packagePathList != null && packagePathList.Count > 0)
		{
			for (int i = 0; i < packagePathList.Count; i++)
			{
				packageSelectDic[packagePathList[i]] = GUILayout.Toggle(packageSelectDic[packagePathList[i]],packagePathList[i]);
			}
		}
        
		EditorGUILayout.EndScrollView();
        if (GUILayout.Button("Import"))
        {
            for (int i = 0; i < packagePathList.Count; i++)
            {
                if(packageSelectDic[packagePathList[i]])
                {
                    string assetPath = "Assets";//AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
                    Package2Folder.ImportPackageToFolder(packagePathList[i], assetPath, true);
                    //break;
                }
            }
        }
	}

    public static void GetFiles(DirectoryInfo directory, string pattern, ref List<string> fileList)
    {
        if (directory != null && directory.Exists && !string.IsNullOrEmpty(pattern))
        {
            try
            {
                foreach (FileInfo info in directory.GetFiles(pattern))
                {
                    string path = info.FullName.ToString();
                    fileList.Add(path);
                }
            }
            catch (System.Exception)
            {
                throw;
            }
            foreach (DirectoryInfo info in directory.GetDirectories())
            {
                GetFiles(info, pattern, ref fileList);
            }
        }
    }

    public class Package2Folder
    {
        #region reflection stuff

        private delegate AssetsItem[] ImportPackageStep1Delegate(string packagePath, out string packageIconPath);

        private static Type assetServerType;

        private static Type AssetServerType
        {
            get
            {
                if (assetServerType == null)
                {
                    assetServerType = typeof(MenuItem).Assembly.GetType("UnityEditor.AssetServer");
                }

                return assetServerType;
            }
        }

        private static ImportPackageStep1Delegate importPackageStep1;

        private static ImportPackageStep1Delegate ImportPackageStep1
        {
            get
            {
                if (importPackageStep1 == null)
                {
                    importPackageStep1 = (ImportPackageStep1Delegate)Delegate.CreateDelegate(
                        typeof(ImportPackageStep1Delegate),
                        null,
                        AssetServerType.GetMethod("ImportPackageStep1"));
                }

                return importPackageStep1;
            }
        }

        private static MethodInfo importPackageStep2MethodInfo;

        private static MethodInfo ImportPackageStep2MethodInfo
        {
            get
            {
                if (importPackageStep2MethodInfo == null)
                {
                    importPackageStep2MethodInfo = AssetServerType.GetMethod("ImportPackageStep2");
                }

                return importPackageStep2MethodInfo;
            }
        }

        private delegate object[] ExtractAndPrepareAssetListDelegate(string packagePath, out string packageIconPath,
            out bool allowReInstall);

        private static Type packageUtilityType;

        private static Type PackageUtilityType
        {
            get
            {
                if (packageUtilityType == null)
                {
                    packageUtilityType
                        = typeof(MenuItem).Assembly.GetType("UnityEditor.PackageUtility");
                }
                return packageUtilityType;
            }
        }

        private static ExtractAndPrepareAssetListDelegate extractAndPrepareAssetList;

        private static ExtractAndPrepareAssetListDelegate ExtractAndPrepareAssetList
        {
            get
            {
                if (extractAndPrepareAssetList == null)
                {
                    extractAndPrepareAssetList
                        = (ExtractAndPrepareAssetListDelegate)Delegate.CreateDelegate(
                            typeof(ExtractAndPrepareAssetListDelegate),
                            null,
                            PackageUtilityType.GetMethod("ExtractAndPrepareAssetList"));
                }

                return extractAndPrepareAssetList;
            }
        }

        private static FieldInfo destinationAssetPathFieldInfo;

        private static FieldInfo DestinationAssetPathFieldInfo
        {
            get
            {
                if (destinationAssetPathFieldInfo == null)
                {
                    Type importPackageItem
                        = typeof(MenuItem).Assembly.GetType("UnityEditor.ImportPackageItem");
                    destinationAssetPathFieldInfo
                        = importPackageItem.GetField("destinationAssetPath");
                }
                return destinationAssetPathFieldInfo;
            }
        }

        private static MethodInfo importPackageAssetsMethodInfo;

        private static MethodInfo ImportPackageAssetsMethodInfo
        {
            get
            {
                if (importPackageAssetsMethodInfo == null)
                {
                    importPackageAssetsMethodInfo
                        = PackageUtilityType.GetMethod("ImportPackageAssetsImmediately") ??
                          PackageUtilityType.GetMethod("ImportPackageAssets");
                }

                return importPackageAssetsMethodInfo;
            }
        }

        private static MethodInfo showImportPackageMethodInfo;

        private static MethodInfo ShowImportPackageMethodInfo
        {
            get
            {
                if (showImportPackageMethodInfo == null)
                {
                    Type packageImport = typeof(MenuItem).Assembly.GetType("UnityEditor.PackageImport");
                    showImportPackageMethodInfo = packageImport.GetMethod("ShowImportPackage");
                }

                return showImportPackageMethodInfo;
            }
        }

        #endregion reflection stuff

        public static void ImportPackageToFolder(string packagePath, string selectedFolderPath, bool interactive)
        {
            string packageIconPath;
            bool allowReInstall;
            if (AssetServerType != null && AssetServerType.GetMethod("ImportPackageStep1") != null)
                IsOlder53VersionAPI = true;
            else
                IsOlder53VersionAPI = false;
            //IsOlder53VersionAPI = false;
            object[] assetsItems = ExtractAssetsFromPackage(packagePath, out packageIconPath, out allowReInstall);
            if (assetsItems == null) return;
            foreach (object item in assetsItems)
            {
                ChangeAssetItemPath(item, selectedFolderPath);
            }

            if (interactive)
            {
                ShowImportPackageWindow(packagePath, assetsItems, packageIconPath, allowReInstall);
            }
            else
            {
                ImportPackageSilently(assetsItems);
            }
        }

        private static bool IsOlder53VersionAPI = false;

        public static object[] ExtractAssetsFromPackage(string path, out string packageIconPath,
            out bool allowReInstall)
        {
            if (IsOlder53VersionAPI)
            {
                AssetsItem[] array = ImportPackageStep1(path, out packageIconPath);
                allowReInstall = false;
                return array;
            }
            else
            {
                object[] array = ExtractAndPrepareAssetList(path, out packageIconPath, out allowReInstall);
                return array;
            }
        }

        private static void ChangeAssetItemPath(object assetItem, string selectedFolderPath)
        {
            if (IsOlder53VersionAPI)
            {
                AssetsItem item = (AssetsItem)assetItem;
                item.exportedAssetPath = selectedFolderPath + item.exportedAssetPath.Remove(0, 6);
                item.pathName = selectedFolderPath + item.pathName.Remove(0, 6);
            }
            else
            {
                string destinationPath
                    = (string)DestinationAssetPathFieldInfo.GetValue(assetItem);
                destinationPath
                    = selectedFolderPath + destinationPath.Remove(0, 6);
                DestinationAssetPathFieldInfo.SetValue(assetItem, destinationPath);
            }
        }

        public static void ShowImportPackageWindow(string path, object[] array, string packageIconPath,
            bool allowReInstall)
        {
            if (IsOlder53VersionAPI)
            {
                ShowImportPackageMethodInfo.Invoke(null, new object[] { path, array, packageIconPath });
            }
            else
            {
                ShowImportPackageMethodInfo.Invoke(null, new object[] { path, array, packageIconPath, allowReInstall });
            }
        }

        public static void ImportPackageSilently(object[] assetsItems)
        {
            if (IsOlder53VersionAPI)
            {
                ImportPackageStep2MethodInfo.Invoke(null, new object[] { assetsItems, false });
            }
            else
            {
                ImportPackageAssetsMethodInfo.Invoke(null, new object[] { assetsItems, false });
            }
        }

        private static string GetSelectedFolderPath()
        {
            UnityEngine.Object obj = Selection.activeObject;
            if (obj == null) return null;
            string path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
            return !Directory.Exists(path) ? null : path;
        }
    }
}
