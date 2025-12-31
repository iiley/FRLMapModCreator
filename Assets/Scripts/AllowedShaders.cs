using System;
using System.Collections.Generic;
using UnityEngine;


public static class AllowedShaders {
    
    private static readonly HashSet<string> s_names = new(StringComparer.Ordinal)
    {
        "FR Legend/Car Outline", 
        "FR Legend/Toon Outline", 
        "FR Legend/SkidMarks", 
        "FR Legend/Hard Edge Unlit", 
        "Projector/Multiply", 
    };
    
    public static bool IsAllowed(string shaderName)
    {
        return s_names.Contains(shaderName);
    }
}
