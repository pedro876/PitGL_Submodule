using System;
using System.Collections.Generic;
using UnityEngine;
using Architecture;

namespace PitGL.IMGUI
{
    public class IMGUI_SubMenu_Cameras : IMGUI_SubMenu
    {
        public override Type GetParentSubMenu() => typeof(IMGUI_SubMenu_Main);

        protected override IEnumerable<IMGUI_Element> EnumerateElements(IntWrapper indent)
        {
            yield return new IMGUI_ElementButton("Camera 0", ()=>Debug.Log("Camera 0"));
            yield return new IMGUI_ElementButton("Camera 1", ()=>Debug.Log("Camera 1"));
            yield return new IMGUI_ElementButton("Camera 2", ()=>Debug.Log("Camera 2"));
        }
    }
}
