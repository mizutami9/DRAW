using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DrawBody.Prototype
{
    [Serializable]
    public sealed class ContentManifestEntry
    {
        public string ResourcePath;
        public string Sha256;
    }

    [Serializable]
    public class UnsignedContentManifest
    {
        public int SchemaVersion;
        public string ProductVersion;
        public ContentManifestEntry[] Entries = Array.Empty<ContentManifestEntry>();
    }

    [Serializable]
    public sealed class SignedContentManifest : UnsignedContentManifest
    {
        public string Signature;
    }

    /// <summary>
    /// Verifies release content before online play. Local stages remain playable
    /// when verification fails, so damaged installations can still show useful UI.
    /// </summary>
    public static class ContentIntegrityVerifier
    {
        public const int CurrentSchemaVersion = 1;
        public const string ProtocolVersion = "pico-online-v1";

        private const string ManifestResourcePath = "Security/content_manifest";
        private const string PublicModulus = "84wD1VH1HZ3n/eIS6F61oxLZtdytxLjcb74KG39VMun+80k3OGYFn7CZRM4ADgJeDJyKyjGAQTkyjiUtrBbzkjVtTyMxFooX+WlDkBBWCxmhuMv393HHABJZ93cZTHpCNGaYWYdjFcxK93QM90Uyt8RhnutLeps3PSKAGWY5VkSeJlDyNMSh4RE6kyHaZW47fMMBNpBuS7AEtVjWuPvrngGCN2DQ4m3ZlDx0yic9wjYY09g/i/i7FWv2jgMipjUXdK5sIPy5+XKyLx34M7bIdbf2chNFsRw/yUfmAviFnPP0tRa6nPmIQ12IzlpquOmE+fCiyPO/lV3vg4901DYaagyPuFx23qHhMqtRQK9XOPULYpCdUcRzSrT8wWPGIfzrBGrnTXK+AaYoctvDIy/sG0Lmhg31o6KM1fXy+kcFS7VbMa15s5w2jjqHA/0CJin3CXc1VUoBMyjLuquzmI92OVFdmiSiUvBANNdhNCwKCrpZpG10bejXwBzvrhGtUTtl";
        private const string PublicExponent = "AQAB";

        private static bool initialized;
        private static bool trusted;
        private static string fingerprint = "unverified";
        private static string failureReason = string.Empty;

        public static bool IsTrusted { get { EnsureInitialized(); return trusted; } }
        public static string Fingerprint { get { EnsureInitialized(); return fingerprint; } }
        public static string FailureReason { get { EnsureInitialized(); return failureReason; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        private static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;

#if UNITY_EDITOR
            trusted = true;
            fingerprint = HashString(ProtocolVersion + "|editor|" + Application.version);
            return;
#else
            VerifyReleaseContent();
#endif
        }

        private static void VerifyReleaseContent()
        {
            try
            {
                TextAsset manifestAsset = Resources.Load<TextAsset>(ManifestResourcePath);
                if (manifestAsset == null)
                {
                    Fail("signed content manifest is missing");
                    return;
                }

                SignedContentManifest signed = JsonUtility.FromJson<SignedContentManifest>(manifestAsset.text);
                if (signed == null || signed.SchemaVersion != CurrentSchemaVersion
                    || string.IsNullOrEmpty(signed.Signature) || signed.Entries == null)
                {
                    Fail("signed content manifest is invalid");
                    return;
                }
                if (!string.Equals(signed.ProductVersion, Application.version, StringComparison.Ordinal))
                {
                    Fail("content manifest product version does not match this player");
                    return;
                }

                UnsignedContentManifest unsigned = new UnsignedContentManifest
                {
                    SchemaVersion = signed.SchemaVersion,
                    ProductVersion = signed.ProductVersion,
                    Entries = signed.Entries
                };
                byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(unsigned, false));
                byte[] signature = Convert.FromBase64String(signed.Signature);
                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters
                    {
                        Modulus = Convert.FromBase64String(PublicModulus),
                        Exponent = Convert.FromBase64String(PublicExponent)
                    });
                    if (!rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    {
                        Fail("content manifest signature does not match");
                        return;
                    }
                }

                HashSet<string> seenPaths = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < signed.Entries.Length; i++)
                {
                    ContentManifestEntry entry = signed.Entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.ResourcePath)
                        || !seenPaths.Add(entry.ResourcePath))
                    {
                        Fail("content manifest contains an invalid path");
                        return;
                    }

                    TextAsset content = Resources.Load<TextAsset>(entry.ResourcePath);
                    if (content == null || !FixedTimeEqualsHex(HashString(NormalizeText(content.text)), entry.Sha256))
                    {
                        Fail("content verification failed: " + entry.ResourcePath);
                        return;
                    }
                }

                trusted = true;
                fingerprint = HashString(ProtocolVersion + "|" + HashBytes(payload) + "|"
                    + HashString(signed.Signature) + "|" + Application.version + "|" + Application.buildGUID);
                failureReason = string.Empty;
            }
            catch (Exception exception)
            {
                Fail(exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void Fail(string reason)
        {
            trusted = false;
            fingerprint = "invalid";
            failureReason = reason;
            Debug.LogError("Content integrity check failed. Online play is disabled; local play remains available. " + reason);
        }

        public static string NormalizeText(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
        }

        public static string HashString(string value)
        {
            return HashBytes(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string HashBytes(byte[] value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(value);
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static bool FixedTimeEqualsHex(string actual, string expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length) return false;
            int difference = 0;
            for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ expected[i];
            return difference == 0;
        }
    }
}
