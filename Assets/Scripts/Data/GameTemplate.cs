﻿using UnityEngine;

namespace Data
{
    [CreateAssetMenu]
    public class GameTemplate : ScriptableObject
    {
        public ProgramTemplate[] AllPrograms;
        public int MaxData;
        public float MemoryIndicationScale;
    }
}