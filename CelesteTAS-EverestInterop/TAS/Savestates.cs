﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Celeste;
using Celeste.Mod.SpeedrunTool.SaveLoad;
using Monocle;
using TAS.EverestInterop;

namespace TAS {
	class Savestates {
		public static CelesteTASModuleSettings Settings => CelesteTASModule.Settings;

		private static InputController savedController;
		public static Coroutine routine;

		public static void HandleSaveStates() {
			if (Hotkeys.hotkeyLoadState == null || Hotkeys.hotkeySaveState == null)
				return;
			if (Manager.Running && Hotkeys.hotkeySaveState.pressed && !Hotkeys.hotkeySaveState.wasPressed) {
				Engine.Scene.OnEndOfFrame += () => {
					if (StateManager.Instance.ExternalSave()) {
						savedController = Manager.controller.Clone();
						routine = new Coroutine(LoadStateRoutine());
					}
				};
			}
			else if (Hotkeys.hotkeyLoadState.pressed && !Hotkeys.hotkeyLoadState.wasPressed && !Hotkeys.hotkeySaveState.pressed) {
				Manager.controller.ReadFile();
				if (StateManager.Instance.SavedPlayer != null 
					&& savedController?.SavedChecksum == Manager.controller.Checksum(savedController.CurrentFrame)) {

					//Fastforward to breakpoint if one exists
					var fastForwards = Manager.controller.fastForwards;
					if (fastForwards.Count > 0 && fastForwards[fastForwards.Count - 1].Line > savedController.Current.Line) {
						Manager.state &= ~State.FrameStep;
						Manager.nextState &= ~State.FrameStep;
					}
					else {
						//InputRecord ff = new InputRecord(0, "***");
						//savedController.fastForwards.Insert(0, ff);
						//savedController.inputs.Insert(savedController.inputs.IndexOf(savedController.Current) + 1, ff);
					}
					Engine.Scene.OnEndOfFrame += () => {
						if (!StateManager.Instance.ExternalLoad())
							return;
						if (!Manager.Running)
							Manager.EnableExternal();
						savedController.inputs = Manager.controller.inputs;
						Manager.controller = savedController.Clone();
						routine = new Coroutine(LoadStateRoutine());
					};
				}
				//If savestate load failed just playback normally
				Manager.DisableExternal();
				Manager.EnableExternal();
			}
			else
				return;
		}

		private static IEnumerator LoadStateRoutine() {
			Manager.forceDelayTimer = 100;
			while (Engine.Scene.Entities.FindFirst<Player>() != null)
				yield return null;
			while (Engine.Scene.Entities.FindFirst<Player>() == null)
				yield return null;
			Manager.forceDelayTimer = 33;
			yield return Engine.DeltaTime;
			Manager.controller.AdvanceFrame(true);
			while (Manager.forceDelayTimer > 1)
				yield return null;
		}
	}
}
