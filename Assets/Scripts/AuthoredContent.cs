using System;
using UnityEngine;

namespace XiuXianShop
{
    // Static imported definitions only. DP-62 / DP-52 consume these; no runtime network or progress lives here.
    public sealed class AuthoredContent : ScriptableObject
    {
        public string lastSnapshotId, lastImportedAtUtc;
        public AuthoredVisit[] visits = Array.Empty<AuthoredVisit>();
        public AuthoredVisitItem[] visitItems = Array.Empty<AuthoredVisitItem>();
        public AuthoredScene[] scenes = Array.Empty<AuthoredScene>();
        public AuthoredNode[] nodes = Array.Empty<AuthoredNode>();
    }

    [Serializable] public abstract class AuthoredRecord
    {
        public string id;
        public bool enabled = true;
    }
    [Serializable] public struct ContentOptionalInt
    {
        public bool hasValue;
        public int value;
    }
    [Serializable] public sealed class AuthoredVisit : AuthoredRecord
    {
        public string title, customerId, displayName, portraitId, type;
        public ContentOptionalInt fixedTurn, earliestTurn, latestTurn, budget;
        public string requiredFlags, forbiddenFlags, prerequisiteVisitId, queuePhase, buyingCategory;
        public int order;
        public string arrivalSceneId, tradeSceneId, skipSceneId, completion;
    }
    [Serializable] public sealed class AuthoredVisitItem : AuthoredRecord
    {
        public string title, visitId, purpose, itemId, instancePresetId;
        public int quantity;
    }
    [Serializable] public sealed class AuthoredScene : AuthoredRecord
    {
        public string title, description, entryNodeId;
    }
    [Serializable] public sealed class AuthoredNode : AuthoredRecord
    {
        public string title, sceneId, type, speaker, text, portraitId, expression;
        public string conditionType, conditionKey, conditionValue, actionType, actionTarget, actionValue;
        public string[] choices = Array.Empty<string>();
        public string[] nextNodeIds = Array.Empty<string>();
    }
}
