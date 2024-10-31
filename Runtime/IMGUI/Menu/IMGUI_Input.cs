using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using System;

namespace PitGL.IMGUI
{
    public static class IMGUI_Input
    {
        public static readonly Input OpenIMGUI = new Input().SetAsCombination().
            SetKeyboard(() => new ButtonControl[]{ Keyboard.current.iKey, Keyboard.current.enterKey }).
            SetGamepad(() => new ButtonControl[] { Gamepad.current.rightShoulder, Gamepad.current.dpad.up });

        public static readonly Input Down = new Input().SetKeyboard(()=>new ButtonControl[] { Keyboard.current.downArrowKey, Keyboard.current.sKey }).SetGamepad(() => Gamepad.current.dpad.down);
        public static readonly Input Up = new Input().SetKeyboard(() => new ButtonControl[] { Keyboard.current.upArrowKey, Keyboard.current.wKey }).SetGamepad(() => Gamepad.current.dpad.up);
        public static readonly Input DownSkip = new Input().SetKeyboard(()=> new ButtonControl[] { Keyboard.current.pageDownKey, Keyboard.current.numpad2Key }).SetGamepad(() => Gamepad.current.rightTrigger);
        public static readonly Input UpSkip = new Input().SetKeyboard(()=> new ButtonControl[] { Keyboard.current.pageUpKey, Keyboard.current.numpad8Key }).SetGamepad(() => Gamepad.current.leftTrigger);
        public static readonly Input Right = new Input().SetKeyboard(() => new ButtonControl[] { Keyboard.current.rightArrowKey, Keyboard.current.dKey }).SetGamepad(() => Gamepad.current.dpad.right);
        public static readonly Input Left = new Input().SetKeyboard(() => new ButtonControl[] { Keyboard.current.leftArrowKey, Keyboard.current.aKey }).SetGamepad(() => Gamepad.current.dpad.left);
        public static readonly Input Accept = new Input().SetKeyboard(() => new ButtonControl[] { Keyboard.current.spaceKey, Keyboard.current.enterKey }).SetGamepad(() => Gamepad.current.buttonSouth);
        public static readonly Input Cancel = new Input().SetKeyboard(() => new ButtonControl[] { Keyboard.current.backspaceKey }).SetGamepad(() => Gamepad.current.buttonEast);
        public static readonly Input MarkFavourite = new Input().SetKeyboard(() => Keyboard.current.mKey).SetGamepad(() => Gamepad.current.leftShoulder);

        public static bool CheckLeftClickHover(Rect rect)
        {
            if (Mouse.current == null) return false;

            Vector2 pos = Mouse.current.position.ReadValue();
            pos.y = Screen.height - pos.y;
            pos = GUI.matrix.inverse.MultiplyPoint(pos);
            bool hovering = rect.Contains(pos);
            Debug.Log($"Hover: {hovering}, rect: {rect}, pos: {pos}");
            return hovering;
        }

        public static bool CheckLeftClickWasPressedThisFrame(Rect rect)
        {
            return CheckLeftClickHover(rect) && Mouse.current.leftButton.wasPressedThisFrame;
        }

        public class Input
        {
            private Func<ButtonControl[]> getKeyboardControls;
            private Func<ButtonControl[]> getGamepadControls;
            private ButtonControl[] keyboardControls;
            private ButtonControl[] gamepadControls;
            private Keyboard keyboard = null;
            private Gamepad gamepad = null;
            private bool combinationTriggered = false;
            private bool isCombination = false; //In combination the order will not matter, except for the last input of the combination.
            private DateTime lastPressDate;
            private bool isHold;
            private bool canceledPress = false;
            private bool canceledPressThisFrame = false;


            #region Initialization

            /// <summary>
            /// Indicates this input that it should be treated as a combination.
            /// The input will be considered pressed if the last input provided was
            /// pressed this frame and the previous inputs are being pressed.
            /// This means that the order of the inputs does not matter as long
            /// as the last input provided if the last input pressed.
            /// </summary>
            /// <returns></returns>
            public Input SetAsCombination()
            {
                this.isCombination = true;
                return this;
            }

            public Input SetKeyboard(Func<ButtonControl> getKeyboardControls)
            {
                this.getKeyboardControls = ()=>new ButtonControl[] { getKeyboardControls() };
                return this;
            }

            public Input SetKeyboard(Func<ButtonControl[]> getKeyboardControls)
            {
                this.getKeyboardControls = getKeyboardControls;
                return this;
            }

            public Input SetGamepad(Func<ButtonControl> getGamepadControls)
            {
                this.getGamepadControls = () => new ButtonControl[] { getGamepadControls() };
                return this;
            }

            public Input SetGamepad(Func<ButtonControl[]> getGamepadControls)
            {
                this.getGamepadControls = getGamepadControls;
                return this;
            }

            #endregion

            /// <summary>
            /// Cancels input until the button is pressed again.
            /// </summary>
            public void CancelPress()
            {
                canceledPress = true;
                canceledPressThisFrame = true;
            }

            /// <summary>
            /// True if the input was pressed this frame.
            /// </summary>
            public bool WasPressed
            {
                get
                {
                    GetControls();
                    if (canceledPressThisFrame)
                    {
                        canceledPressThisFrame = false;
                        return false;
                    }
                    bool wasPressed = false;
                    if (Keyboard.current != null && keyboardControls != null)
                    {
                        wasPressed |= CheckWasPressedThisFrame(keyboardControls);
                    }
                    if (Gamepad.current != null && gamepadControls != null)
                    {
                        wasPressed |= CheckWasPressedThisFrame(gamepadControls);
                    }
                    if (wasPressed)
                    {
                        lastPressDate = DateTime.Now;
                        canceledPress = false;
                    }
                    return wasPressed;
                }
            }

            /// <summary>
            /// True if the input is being pressed.
            /// </summary>
            public bool IsPressed
            {
                get
                {
                    GetControls();
                    if (canceledPress) return false;
                    bool isPressed = false;
                    if (Keyboard.current != null && keyboardControls != null)
                    {
                        isPressed |= CheckIsPressed(keyboardControls);
                    }
                    if (Gamepad.current != null && gamepadControls != null)
                    {
                        isPressed |= CheckIsPressed(gamepadControls);
                    }
                    return isPressed;
                }
            }

            /// <summary>
            /// True on selected frames periodically if the input is being hold.
            /// It's not true every frame because that motion would be framerate dependant
            /// and most likely too fast.
            /// </summary>
            public bool IsHold
            {
                get
                {
                    GetControls();
                    if (canceledPress) return false;
                    DateTime now = DateTime.Now;
                    double span = (now - lastPressDate).TotalSeconds;
                    if(IsPressed)
                    {
                        if (!isHold && span > 0.4f)
                        {
                            //Once it's been pressed for a certain amount, start considering that this input is being hold
                            isHold = true;
                            lastPressDate = now;
                            return true;
                        }
                        else if (isHold && span > 0.07f)
                        {
                            //Once it's been hold for a while, return true periodically
                            lastPressDate = now;
                            return true;
                        }
                        else return false;
                    }
                    else
                    {
                        isHold = false;
                        return false;
                    }
                }
            }

            private void GetControls()
            {
                if(getKeyboardControls != null)
                {
                    if (keyboard != Keyboard.current)
                    {
                        if (Keyboard.current == null) keyboardControls = null;
                        else keyboardControls = getKeyboardControls();
                        keyboard = Keyboard.current;
                    }
                }
                if (getGamepadControls != null)
                {
                    if (gamepad != Gamepad.current)
                    {
                        if (Gamepad.current == null) gamepadControls = null;
                        else gamepadControls = getGamepadControls();
                        gamepad = Gamepad.current;
                    }
                }

            }

            private bool CheckWasPressedThisFrame(ButtonControl[] controls)
            {
                if(isCombination)
                {
                    bool areAllButLastPressed = true;
                    for (int i = 0; i < controls.Length - 1; i++)
                    {
                        if (!controls[i].isPressed) areAllButLastPressed = false;
                    }

                    //If any input fails we indicate the the combination is no longer triggered
                    bool areAllPressed = areAllButLastPressed && controls[controls.Length - 1].isPressed;
                    if (!areAllPressed)
                    {
                        combinationTriggered = false;
                    }

                    //If previous inputs are pressed and the last is pressed this frame, we return true
                    if (areAllButLastPressed && controls[controls.Length - 1].wasPressedThisFrame)
                    {
                        combinationTriggered = true;
                        return true;
                    }
                    else return false;
                }
                else
                {
                    for (int i = 0; i < controls.Length; i++)
                    {
                        if (controls[i].wasPressedThisFrame) return true;
                    }
                    return false;
                }
            }

            private bool CheckIsPressed(ButtonControl[] controls)
            {
                if(isCombination)
                {
                    if (controls.Length == 1) return controls[0].isPressed;
                    else
                    {
                        //If any input fails we indicate the the combination is no longer triggered
                        for (int i = 0; i < controls.Length; i++)
                        {
                            if (!controls[i].isPressed) combinationTriggered = false;
                        }
                        return combinationTriggered;
                    }
                }
                else
                {
                    for (int i = 0; i < controls.Length; i++)
                    {
                        if (controls[i].isPressed) return true;
                    }
                    return false;
                }
                
            }

        }

        
    }
}
