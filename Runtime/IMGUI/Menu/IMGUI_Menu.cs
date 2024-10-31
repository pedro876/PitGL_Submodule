using System.Collections.Generic;
using UnityEngine;
using System;
using System.Reflection;

namespace PitGL.IMGUI
{
    public class IMGUI_Menu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; } = false;

        private const float MENU_WIDTH_PCT = 0.36f;
        private const float OPACITY = 0.9f;

        private IMGUI_SubMenu currentSubMenu;
        private IMGUI_SubMenu mainSubMenu;
        private Dictionary<Type, IMGUI_SubMenu> subMenus = new ();
        

        private void Awake()
        {
            //GENERATE ALL SUBMENUS IN ADVANCE, THIS ENSURES THAT THE FAVOURITES SUBMENU WORKS PROPERLY
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            Type baseType = typeof(IMGUI_SubMenu);
            foreach(var type in types)
            {
                if (!subMenus.ContainsKey(type) && type.IsSubclassOf(baseType) && !type.IsAbstract)
                {
                    CreateSubmenuInstance(type);
                }
            }

            mainSubMenu = subMenus[typeof(IMGUI_SubMenu_Main)];
            currentSubMenu = mainSubMenu;
        }

        private void OnDestroy()
        {
            foreach(var submenu in subMenus.Values)
            {
                submenu.Destroy();
            }
        }

        #region Logic

        public void SetSubMenu<T>(bool shouldResetSelection = true) where T : IMGUI_SubMenu
        {
            SetSubMenu(typeof(T), shouldResetSelection);
        }

        public void SetSubMenu(Type type, bool shouldResetSelection = true)
        {
            currentSubMenu?.Exit();
            if (!subMenus.TryGetValue(type, out currentSubMenu))
            {
                Debug.LogWarning($"IMGUI Submenu was not created in advance for some reason ({type.Name})");
                CreateSubmenuInstance(type);
            }
            currentSubMenu.Enter(shouldResetSelection);
        }

        private void CreateSubmenuInstance(Type type)
        {
            IMGUI_SubMenu instance = Activator.CreateInstance(type) as IMGUI_SubMenu;
            instance.menu = this;
            subMenus.Add(type, instance);
        }

        public void Open()
        {
            IsOpen = true;
            currentSubMenu?.Enter(shouldResetSelection: false);
            //SetSubMenu<IMGUI_SubMenu_Main>(shouldResetSelection: false);
        }

        public void Close()
        {
            IsOpen = false;
            currentSubMenu?.Exit();
        }

        private void Update()
        {
            if(IMGUI_Input.OpenIMGUI.WasPressed)
            {
                if (IsOpen) Close();
                else Open();
                IMGUI_Input.Up.CancelPress();
                IMGUI_Input.Down.CancelPress();
                IMGUI_Input.UpSkip.CancelPress();
                IMGUI_Input.DownSkip.CancelPress();
                return;
            }
            else if(IsOpen && IMGUI_Input.Cancel.WasPressed)
            {
                Type parentSubMenu = currentSubMenu.GetParentSubMenu();
                if (parentSubMenu == null) Close();
                else SetSubMenu(parentSubMenu, shouldResetSelection: false);
                return;
            }

            if (!IsOpen) return;
            if (currentSubMenu != null)
            {
                currentSubMenu.Update();
            }
        }

        #endregion

        #region GUI

        private void OnGUI()
        {
            if (!IsOpen) return;
            //Matrix4x4 ogMatrix = GUI.matrix;
            //float scaleFactor = Screen.height / SCREEN_HEIGHT;
            //GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width - SCREEN_WIDTH * scaleFactor, 0f, 0f), Quaternion.identity, new Vector3(scaleFactor, scaleFactor, 1f));
            //
            //Rect rect = new Rect(SCREEN_WIDTH - MENU_WIDTH, 0f, MENU_WIDTH, SCREEN_HEIGHT);
            //GUI.Box(rect, "", IMGUI_Style.GetBoxStyle(new Color(0,0,0,OPACITY)));
            IMGUI_Layout.InsideBox(MENU_WIDTH_PCT, 1f, IMGUI_Layout.Anchor.TopRight, (rect) =>
            {
                DrawContent(rect);
            }, opacity: OPACITY, padding: 0f);
            
            

            //GUI.matrix = ogMatrix;
        }

        private Rect DrawContent(Rect rect)
        {
            if(currentSubMenu != null)
            {
                rect = currentSubMenu.DrawContent(rect);
            }
            
            return rect;
        }

        #endregion
    }
}
