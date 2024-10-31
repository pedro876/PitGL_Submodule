using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PitGL.AI
{
	public abstract class HState<TContext> where TContext : class
    {
		private static int NextId = 0;
		protected readonly int id;

		private Dictionary<int, HState<TContext>> dict_subStates;
		private HState<TContext>[] arr_subStates;
		private HState<TContext> currentSubState = null;

		public HState<TContext> parent;
		protected TContext context;

		public HState()
		{
			Type type = GetType();
			FieldInfo fieldInfo = type.GetField("Id", BindingFlags.Static | BindingFlags.Public);
			if(fieldInfo == null)
			{
				Debug.LogError($"The HState {type.Name} doesn't define a static id");
			}
			else
			{
				int currentId = (int)fieldInfo.GetValue(null);
				if(currentId <= 0)
				{
		            NextId++;
		            fieldInfo.SetValue(null, NextId);
		            this.id = NextId;
		        }
				else
				{
					this.id = currentId;
				}			
		    }
		}

		public HState<TContext> SetChildren(HState<TContext>[] children) 
		{
			arr_subStates = children;
			dict_subStates = new Dictionary<int, HState<TContext>>();

			for (int i = 0; i < children.Length; i++) 
			{
				dict_subStates.Add(children[i].id, children[i]);
				children[i].parent = this;
			}

			return this;
		}

		public HState<TContext> SetContextDeep(TContext context) 
		{
			this.context = context;
			OnInitialized();
			if(arr_subStates != null)
			{
                for (int i = 0; i < arr_subStates.Length; i++)
                {
                    arr_subStates[i].SetContextDeep(context);
                }
            }

			return this;
        }

		#region State

		public HState<TContext> Enter(int id) 
		{
			currentSubState?.Exit();

			if(dict_subStates.TryGetValue(id, out currentSubState)) 
			{
				currentSubState.OnEnter();
				return currentSubState;
			}
			else 
			{
				Debug.LogWarning($"[HState] Substate not found at {GetType().Name} with target id {id}");
				return null;
			}
		}

		public HState<TContext> GetLeaf() 
		{
			if (currentSubState == null) return this;
			else return currentSubState.GetLeaf();
		}

		public bool IsCurrentState(int id) 
		{
			if (this.id == id) return true;
			else if (currentSubState != null) return currentSubState.IsCurrentState(id);
			else return false;
		}

		public int CurrentId() => currentSubState.id;

        #endregion

        #region Execution

        public void Update() 
		{
			OnUpdate();
			currentSubState?.Update();
		}

		public void LateUpdate() 
		{
			OnLateUpdate();
			currentSubState?.LateUpdate();
		}

		public void FixedUpdate() 
		{
			OnFixedUpdate();
			currentSubState?.FixedUpdate();
		}

		public void Exit() 
		{
			currentSubState?.Exit();
			OnExit();
			currentSubState = null;
		}

		#endregion

		#region Events

		protected virtual void OnInitialized() { }
		protected virtual void OnEnter() { }
		protected virtual void OnResume() { }
		protected virtual void OnUpdate() { }
		protected virtual void OnLateUpdate() { }
		protected virtual void OnFixedUpdate() { }
		protected virtual void OnExit() { }

		#endregion
	}
}
