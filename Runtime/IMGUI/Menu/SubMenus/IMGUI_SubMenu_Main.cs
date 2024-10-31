using System;
using System.Collections.Generic;
using UnityEngine;
using Architecture;

namespace PitGL.IMGUI
{
    public class IMGUI_SubMenu_Main : IMGUI_SubMenu
    {

        public IMGUI_SubMenu_Main() : base("IMGUI MENU")
        {
            
        }

        public override Type GetParentSubMenu() => null;

        protected override IEnumerable<IMGUI_Element> EnumerateElements(IntWrapper indent)
        {
            yield return new IMGUI_ElementButton("Favourites", () => menu.SetSubMenu<IMGUI_SubMenu_Favourites>()) { canBeMarkedAsFavourite = false };
            yield return new IMGUI_ElementButton("Graphics", () => menu.SetSubMenu<IMGUI_SubMenu_Graphics>()) { canBeMarkedAsFavourite = false };
        }
    }
}
