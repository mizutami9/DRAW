#if NICO_DRAW_STEAM && !DISABLESTEAMWORKS && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
using System;
using Steamworks;
using UnityEngine;

namespace DrawBody.Prototype
{
    /// <summary>
    /// Owns the Steamworks client lifetime and exposes read-only authentication
    /// tickets to EOS Connect. Gameplay continues to use the normal EOS network path.
    /// </summary>
    internal sealed class SteamPlatformAuth : MonoBehaviour
    {
        internal const uint AppId = 5230890;
        private const int InitialTicketBufferSize = 1024;
        private const string EosTicketAudience = "epiconlineservices";

        private static SteamPlatformAuth instance;
        private HAuthTicket activeTicket = HAuthTicket.Invalid;
        private bool initialized;

        internal static bool IsAvailable => instance != null && instance.initialized;
        internal static string SteamId => IsAvailable ? SteamUser.GetSteamID().ToString() : string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            GameObject owner = new GameObject("SteamPlatformAuth");
            DontDestroyOnLoad(owner);
            instance = owner.AddComponent<SteamPlatformAuth>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

#if !UNITY_EDITOR
            if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
            {
                Debug.Log("[NICO DRAW Steam] Relaunching through the Steam client.");
                Application.Quit();
                return;
            }
#endif

            try
            {
                initialized = SteamAPI.Init();
                if (!initialized)
                {
                    Debug.LogError("[NICO DRAW Steam] SteamAPI.Init failed. Start Steam and launch NICO DRAW from its Library.");
                    return;
                }

                Debug.Log("[NICO DRAW Steam] Steam initialized for App ID " + AppId + ", user " + SteamId + ".");
            }
            catch (Exception ex)
            {
                initialized = false;
                Debug.LogError("[NICO DRAW Steam] Initialization failed: " + ex.Message);
            }
        }

        private void Update()
        {
            if (!initialized) return;
            SteamAPI.RunCallbacks();
        }

        internal static void RequestEosTicket(Action<bool, string, string> completed)
        {
            if (!IsAvailable)
            {
                completed?.Invoke(false, null, "Steam is not initialized.");
                return;
            }
            instance.BeginTicketRequest(completed);
        }

        private void BeginTicketRequest(Action<bool, string, string> completed)
        {
            CancelTicket();
            int bufferSize = InitialTicketBufferSize;
            byte[] buffer = new byte[bufferSize];
            uint ticketSize;
            SteamNetworkingIdentity audience = new SteamNetworkingIdentity();
            audience.m_eType = ESteamNetworkingIdentityType.k_ESteamNetworkingIdentityType_GenericString;
            audience.SetSteamID(SteamUser.GetSteamID());
            audience.SetGenericString(EosTicketAudience);

            activeTicket = SteamUser.GetAuthSessionTicket(buffer, bufferSize, out ticketSize, ref audience);
            if (activeTicket == HAuthTicket.Invalid || ticketSize == 0)
            {
                CancelTicket();
                completed?.Invoke(false, null, "Steam did not create an EOS session ticket.");
                return;
            }

            if (ticketSize > bufferSize)
            {
                CancelTicket();
                bufferSize = checked((int)ticketSize);
                buffer = new byte[bufferSize];
                activeTicket = SteamUser.GetAuthSessionTicket(buffer, bufferSize, out ticketSize, ref audience);
                if (activeTicket == HAuthTicket.Invalid || ticketSize == 0 || ticketSize > buffer.Length)
                {
                    CancelTicket();
                    completed?.Invoke(false, null, "Steam returned an invalid EOS session ticket.");
                    return;
                }
            }

            Array.Resize(ref buffer, checked((int)ticketSize));
            string token = BitConverter.ToString(buffer).Replace("-", string.Empty);
            Debug.Log("[NICO DRAW Steam] Created EOS session ticket (" + ticketSize
                + " bytes, audience " + EosTicketAudience + ").");
            completed?.Invoke(true, token, null);
        }

        internal static void ReleaseTicket()
        {
            instance?.CancelTicket();
        }

        private void CancelTicket()
        {
            if (!initialized || activeTicket == HAuthTicket.Invalid) return;
            SteamUser.CancelAuthTicket(activeTicket);
            activeTicket = HAuthTicket.Invalid;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            CancelTicket();
            if (initialized) SteamAPI.Shutdown();
            initialized = false;
            instance = null;
        }
    }
}
#endif
