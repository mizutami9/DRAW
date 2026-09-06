using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using DrawBody.Prototype;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DrawBody.EditorTools
{
    public sealed class ContentManifestBuildProcessor : IPreprocessBuildWithReport
    {
        private const string ManifestAssetPath = "Assets/Resources/Security/content_manifest.json";
        private const string SigningKeyEnvironmentVariable = "PICO_CONTENT_SIGNING_KEY";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            GenerateSignedManifestOrThrow();
        }

        [MenuItem("PICO/Security/Regenerate Signed Content Manifest")]
        public static void GenerateSignedManifestMenu()
        {
            GenerateSignedManifestOrThrow();
            Debug.Log("Signed content manifest regenerated.");
        }

        public static void GenerateSignedManifestOrThrow()
        {
            string privateKeyPath = ResolvePrivateKeyPath();
            if (!File.Exists(privateKeyPath))
            {
                throw new BuildFailedException(
                    "The content signing private key was not found. Set " + SigningKeyEnvironmentVariable
                    + " or place the protected key at: " + privateKeyPath);
            }

            List<ContentManifestEntry> entries = new List<ContentManifestEntry>();
            AddJsonResources(entries, "Assets/Resources/Stages", "Stages");
            AddJsonResources(entries, "Assets/Resources/Localization", "Localization");
            entries.Sort((left, right) => string.CompareOrdinal(left.ResourcePath, right.ResourcePath));
            if (entries.Count == 0) throw new BuildFailedException("No protected stage/localization content was found.");

            UnsignedContentManifest unsigned = new UnsignedContentManifest
            {
                SchemaVersion = ContentIntegrityVerifier.CurrentSchemaVersion,
                ProductVersion = PlayerSettings.bundleVersion,
                Entries = entries.ToArray()
            };
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(unsigned, false));
            byte[] signature;
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(ReadPrivateKey(privateKeyPath));
                signature = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }

            SignedContentManifest signed = new SignedContentManifest
            {
                SchemaVersion = unsigned.SchemaVersion,
                ProductVersion = unsigned.ProductVersion,
                Entries = unsigned.Entries,
                Signature = Convert.ToBase64String(signature)
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestAssetPath));
            File.WriteAllText(ManifestAssetPath, JsonUtility.ToJson(signed, true) + Environment.NewLine, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(ManifestAssetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AddJsonResources(List<ContentManifestEntry> entries, string directory, string prefix)
        {
            if (!Directory.Exists(directory)) return;
            string[] files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = files[i].Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string resourcePath = prefix + "/" + Path.ChangeExtension(relative, null).Replace('\\', '/');
                string text = ContentIntegrityVerifier.NormalizeText(File.ReadAllText(files[i], Encoding.UTF8));
                entries.Add(new ContentManifestEntry
                {
                    ResourcePath = resourcePath,
                    Sha256 = ContentIntegrityVerifier.HashString(text)
                });
            }
        }

        private static string ResolvePrivateKeyPath()
        {
            string configured = Environment.GetEnvironmentVariable(SigningKeyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PICO", "BuildSecurity", "content-signing-private.xml");
        }

        private static RSAParameters ReadPrivateKey(string path)
        {
            XmlDocument document = new XmlDocument();
            document.Load(path);
            RSAParameters parameters = new RSAParameters
            {
                Modulus = ReadRequired(document, "Modulus"),
                Exponent = ReadRequired(document, "Exponent"),
                P = ReadRequired(document, "P"),
                Q = ReadRequired(document, "Q"),
                DP = ReadRequired(document, "DP"),
                DQ = ReadRequired(document, "DQ"),
                InverseQ = ReadRequired(document, "InverseQ"),
                D = ReadRequired(document, "D")
            };
            return parameters;
        }

        private static byte[] ReadRequired(XmlDocument document, string name)
        {
            XmlNode node = document.DocumentElement?.SelectSingleNode(name);
            if (node == null || string.IsNullOrWhiteSpace(node.InnerText))
                throw new BuildFailedException("The signing key is missing " + name + ".");
            return Convert.FromBase64String(node.InnerText);
        }
    }
}
