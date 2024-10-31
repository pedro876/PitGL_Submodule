using System;
using System.Collections.Generic;
using UnityEngine;
using Architecture;

namespace PitGL.IMGUI
{
    public class IMGUI_SubMenu_Favourites : IMGUI_SubMenu
    {
        public override Type GetParentSubMenu() => typeof(IMGUI_SubMenu_Main);

        protected override IEnumerable<IMGUI_Element> EnumerateElements(IntWrapper indent)
        {
            foreach(var element in IMGUI_ElementInteractable.favourites)
            {
                yield return element;
            }
        }

        protected override void OnEnter()
        {
            base.OnEnter();
            CreateElements();
        }
    }
}
