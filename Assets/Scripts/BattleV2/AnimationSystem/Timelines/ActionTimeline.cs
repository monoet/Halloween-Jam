using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.AnimationSystem.Timelines
{
    [CreateAssetMenu(menuName = "Battle/Animation/Action Timeline", fileName = "NewActionTimeline")]
    public sealed class ActionTimeline : ScriptableObject
    {
        [SerializeField] private string actionId;
        [SerializeField] private string displayName;
        [SerializeField] private Metadata metadata = new();
        [SerializeField] private List<Track> tracks = new();

        public string ActionId => actionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? actionId : displayName;
        public Metadata Info => metadata;
        public IReadOnlyList<Track> Tracks => tracks;

        [Serializable]
        public struct Metadata
        {
            [Tooltip("Duracion total normalizada (0..1) -> se escala segun velocidad y clock.")]
            public float Length;

            [Tooltip("Etiquetas libres para filtros (por ejemplo: melee, magic, projectile).")]
            public string[] Tags;
        }

        [Serializable]
        public struct Track
        {
            [SerializeField] private TrackType type;
            [SerializeField] private List<Phase> phases;

            public TrackType Type => type;
            public IReadOnlyList<Phase> Phases => phases;
        }

        [Serializable]
        public struct Phase
        {
            [Range(0f, 1f)]
            public float Start;

            [Range(0f, 1f)]
            public float End;

            [Tooltip("Evento emitido al comenzar la fase.")]
            public string OnEnterEvent;

            [Tooltip("Evento emitido al finalizar la fase.")]
            public string OnExitEvent;

            [Tooltip("Payload adicional para routers (por ejemplo VFX, SFX).")]
            public string Payload;
        }

        public enum TrackType
        {
            Animation,
            Impact,
            Window,
            Lock,
            Custom
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var assetName = name?.Trim();
            if (!string.IsNullOrEmpty(assetName) && !string.Equals(actionId, assetName, StringComparison.Ordinal))
            {
                actionId = assetName;
            }
        }
#endif
    }
}
