using ModSyncNinjaApiBridge;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Verse;

namespace RimWorld_ModSyncNinja
{
    class FileUtil
    {
        private const string ABOUT_DIRECTORY = "About";
        private const string ASSEMBLIES_DIRECTORY = "Assemblies";

        private const string ABOUT_XML = "About.xml";
        private const string MOD_SYNC_XML = "ModSync.xml";
        private const string PUBLISHED_FIELD_TXT = "PublishedFileId.txt";

        private const string ABOUT_ROOT_ELEMENT = "ModMetaData";
        private const string MODSYNC_NINJA_ROOT_ELEMENT = "ModSyncNinjaData";

        private const string VERSION_ELEMENT = "Version";
        private const string SAVE_BREAKING_ELEMENT = "SaveBreaking";

        private readonly static HashSet<string> DllsToExclude = new HashSet<string>();

        static FileUtil()
        {
            DllsToExclude.Add(@"$HugsLibChecker.dll");
            DllsToExclude.Add(@"0Harmony.dll");
            DllsToExclude.Add(@"SaveStorageSettingsUtil.dll");
            DllsToExclude.Add(@"ModSyncNinjaApiBridge.dll");
        }

        public static string GetSteamPublishedField(DirectoryInfo rootDir)
        {
            DirectoryInfo aboutDir = rootDir.GetDirectories(ABOUT_DIRECTORY).FirstOrDefault();
            if (aboutDir == null) return String.Empty;

            FileInfo file = aboutDir.GetFiles(PUBLISHED_FIELD_TXT).FirstOrDefault();
            if (file == null) return String.Empty;

            return File.ReadAllText(file.FullName);
        }

        private delegate void OpenXmlFromAboutDirCallback(XElement root);
        private static bool OpenXmlFromAboutDir(DirectoryInfo rootDir, string xmlFileToOpen, FileAccess fileAccess, OpenXmlFromAboutDirCallback callback)
        {
            if (callback == null)
                throw new Exception("callback cannot be null");

            DirectoryInfo aboutDir = rootDir.GetDirectories(ABOUT_DIRECTORY).FirstOrDefault();
            if (aboutDir == null) return false;

            FileInfo xmlFile = aboutDir.GetFiles(MOD_SYNC_XML).FirstOrDefault();
            if (xmlFile == null) return false;

            try
            {
                using (FileStream fs = xmlFile.Open(FileMode.Open, fileAccess))
                {
                    XDocument xml = null;
                    using (StreamReader sr = new StreamReader(fs))
                    {
                        xml = XDocument.Parse(sr.ReadToEnd());
                        if (!xml.Elements().Any())
                        {
                            throw new Exception("Invalid markup, null elements");
                        }
                        string rootElement = (ABOUT_XML.Equals(xmlFileToOpen)) ? ABOUT_ROOT_ELEMENT : MODSYNC_NINJA_ROOT_ELEMENT;
                        XElement root = xml.Element(rootElement);
                        if (root == null)
                        {
                            throw new Exception("Invalid markup, missing root");
                        }
                        callback.Invoke(root);
                    }
                    if (xml != null && 
                        (fileAccess == FileAccess.ReadWrite || fileAccess == FileAccess.Write))
                    {
                        xml.Save(xmlFile.FullName);
                    }
                }
                return true;
            }
            catch(Exception e)
            {
                MSLog.Log(xmlFileToOpen + ": " + e.Message, MSLog.Level.All, true);
                return false;
            }
        }

        public static string GetModSyncVersionForMod(DirectoryInfo rootDir)
        {
            string version = String.Empty;
            OpenXmlFromAboutDir(rootDir, MOD_SYNC_XML, FileAccess.Read, (XElement root) =>
            {
                var xElement = root.Element(VERSION_ELEMENT);
                if (xElement != null)
                {
                    version = xElement.Value;
                }
            });
            return version;
        }

        public static string GetModSyncId(DirectoryInfo rootDir)
        {
            string id = String.Empty;
            OpenXmlFromAboutDir(rootDir, MOD_SYNC_XML, FileAccess.Read, (XElement root) =>
            {
                var xElement = root.Element("ID");
                if (xElement != null)
                {
                    id = xElement.Value;
                }
            });
            return id;
        }

        public static bool UpdateModSyncXml(DirectoryInfo rootDir, UpdateModRequest request)
        {
            return OpenXmlFromAboutDir(rootDir, MOD_SYNC_XML, FileAccess.ReadWrite, (XElement root) =>
            {
                AddOrUpdateContent(root, VERSION_ELEMENT, request.Version);
                AddOrUpdateContent(root, SAVE_BREAKING_ELEMENT, request.SaveBreaking.ToString());
            });
        }

        private static void AddOrUpdateContent(XElement parent, string contentName, string contentValue)
        {
            var xElement = parent.Element(contentName);
            if (xElement == null)
            {
                xElement = new XElement(contentName, contentValue);
                parent.Add(xElement);
            }
            else
            {
                xElement.Value = contentValue;
            }
        }

        public static string GetVersionFromDll(ModMetaData mod)
        {
            string assemblyDirectory = mod.RootDir + "/" + ASSEMBLIES_DIRECTORY;
            if (!Directory.Exists(assemblyDirectory))
                return null;

            string foundDll = null;
            foreach(string dll in Directory.GetFiles(assemblyDirectory))
            {
                if (!DllsToExclude.Contains(Path.GetFileName(dll)))
                {
                    foundDll = dll;
                    break;
                }
            }

            if (!String.IsNullOrEmpty(foundDll))
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(@foundDll);
                return info.FileVersion;
            }

            return null;
        }

        public static string GetAboutFileText(DirectoryInfo rootDir)
        {
            DirectoryInfo aboutDir = rootDir.GetDirectories(ABOUT_DIRECTORY).FirstOrDefault();
            if (aboutDir == null) return String.Empty;
            FileInfo fi = aboutDir.GetFiles(ABOUT_XML).FirstOrDefault();
            if (!fi.Exists) return String.Empty;
            return File.ReadAllText(fi.FullName);
        }
    }
}
