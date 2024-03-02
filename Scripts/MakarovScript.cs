using HarmonyLib;
using Receiver2;
using Receiver2ModdingKit;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using UnityEngine;

public class MakarovScript : ModGunScript
{
	private readonly float[] kHammerSlideCurve = new float[]
	{
		0f, 0f,
		0.4f, 0.9f,
		0.5f, 0.95f
	};

	private readonly float[] kSearTriggerCurve = new float[]
	{
		0f, 0f,
		1f, 1f
	};

	private bool decocking;

	private bool slide_push_hammer;

	private RotateMover sear;

	private float sear_almost_cocked;

	private float sear_cocked;

	private float sear_halfcocked;

	private float sear_safety_on;

	private float sear_hammer_back;

	private float sear_uncocked;

	private float[] kSearSafetyCurve = new float[]
	{
		0f, 0f,
		1f, 1f
	};

	private float[] kHammerTriggerCurve = new float[]
	{
		0f ,0f,
		1f, 1f
	};

	private bool hammer_rest;

	public SkinnedMeshRenderer main_spring;

	public AnimationCurve da_animation_curve = new AnimationCurve(new Keyframe[]
	{
		new Keyframe(0f, 0f),
		new Keyframe(1f, 1f),
	});

	public AnimationCurve sa_animation_curve = new AnimationCurve(new Keyframe[]
	{
		new Keyframe(0f, 0f),
		new Keyframe(1f, 1f),
	});

	private bool isInDoubleActionMode;

	private bool is_da_pulling_trigger;

	private bool da_needs_reset;

	private float interpCurveSlideHammer;

	public override void InitializeGun()
	{
		loaded_cartridge_prefab.GetComponent<ShellCasingScript>().ammo_box_prefab = ((ShellCasingScript)ReceiverCoreScript.Instance().generic_prefabs.First(item => item is ShellCasingScript shellPrefab && shellPrefab.cartridge_type == CartridgeSpec.Preset._9mm && shellPrefab.name != "load_progression")).ammo_box_prefab;

		pooled_muzzle_flash = ((GunScript)ReceiverCoreScript.Instance().generic_prefabs.First(item => item is GunScript gun && gun.gun_model == GunModel.m1911)).pooled_muzzle_flash;
	}

	public override void AwakeGun()
	{
		sear = AccessTools.FieldRefAccess<GunScript, RotateMover>(this, "sear");

		sear_almost_cocked = AccessTools.FieldRefAccess<GunScript, float>(this, "sear_almost_cocked");

		sear_cocked = AccessTools.FieldRefAccess<GunScript, float>(this, "sear_cocked");

		sear_halfcocked = AccessTools.FieldRefAccess<GunScript, float>(this, "sear_halfcocked");

		sear_hammer_back = AccessTools.FieldRefAccess<GunScript, float>(this, "sear_hammer_back");

		sear_uncocked = AccessTools.FieldRefAccess<GunScript, float>(this, "sear_uncocked");

		sear_safety_on = Quaternion.Angle(this.sear.rotations[0], base.transform.Find("sear_safety_on").localRotation) / Quaternion.Angle(this.sear.rotations[0], this.sear.rotations[1]);

		kSearSafetyCurve[3] = sear_safety_on;
	}

	public override void UpdateGun()
	{
		// Decocking logic (I'm pretty sure the game does this on its own but it didn't work when I first tried it but when I removed this section of code it still worked idk why I just hate it here man for fuck's sake)
		if (player_input.GetButton(14) && player_input.GetButtonDown(2) && slide.amount == 0f) decocking = true;

		if (isInDoubleActionMode) base_trigger_profile = da_animation_curve; //harder pull when in double action, as the gun doesn't have a hair trigger mode
		else base_trigger_profile = sa_animation_curve;

		is_da_pulling_trigger = isInDoubleActionMode && LocalAimHandler.player_instance.PullingTrigger && !da_needs_reset && !IsSafetyOn();

		interpCurveSlideHammer = InterpCurve(kHammerSlideCurve, slide.amount);
		if (slide.amount > 0f) //makes the hammer go to its max value
		{
			slide_push_hammer = true;
		}
		else
		{
			slide_push_hammer = false;
		}

		if (hammer.amount < _hammer_halfcocked)
		{
			sear.amount = sear_uncocked;
		}
		else if (hammer.amount < _hammer_cocked_val)
		{
			sear.amount = Mathf.Lerp(sear_halfcocked, sear_almost_cocked, (hammer.amount - _hammer_halfcocked) / (_hammer_cocked_val - _hammer_halfcocked));
		}
		else
		{
			sear.amount = Mathf.Lerp(sear_cocked, sear_hammer_back, (hammer.amount - _hammer_cocked_val) / (1f - _hammer_cocked_val));
		}

		var interpCurveSearTrigger = InterpCurve(kSearTriggerCurve, trigger.amount);
		if (interpCurveSearTrigger > sear.amount && !slide_push_hammer && !_disconnector_needs_reset) sear.amount = interpCurveSearTrigger;
		else if (slide_push_hammer) sear.amount = Mathf.MoveTowards(sear.amount, sear_cocked, Time.deltaTime * 5);

		hammer.asleep = slide_push_hammer && is_da_pulling_trigger;

		if (!slide_push_hammer && !_disconnector_needs_reset && _hammer_state != 2 && !decocking) isInDoubleActionMode = true;

		var interpCurveHammerTrigger = InterpCurve(kHammerTriggerCurve, trigger.amount);

		if (isInDoubleActionMode && interpCurveHammerTrigger > hammer.amount && !da_needs_reset) hammer.amount = interpCurveHammerTrigger;

		if (hammer.amount > _hammer_cocked_val)
		{
			if (_hammer_state == 1) _hammer_state = 2;
			isInDoubleActionMode = false;
			da_needs_reset = true;
		}

		if (decocking)
		{
			if (!player_input.GetButton(14))
			{
				hammer.amount = Mathf.MoveTowards(hammer.amount, (IsSafetyOn()) ? _hammer_halfcocked : 0f, Time.deltaTime * 5);
			}
			if (hammer.amount == ((IsSafetyOn()) ? _hammer_halfcocked : 0) || !player_input.GetButton(2))
			{
				_hammer_state = 0;
				decocking = false;

				AudioManager.PlayOneShotAttached(sound_decock, hammer.transform.gameObject, 0.3f);
			}
		}
		if (!decocking)
		{
			if ((_hammer_state == 1 && hammer.amount != _hammer_halfcocked) || (_hammer_state == 2 && hammer.amount != _hammer_cocked_val)) hammer_rest = false; 

			bool isHammerFree = !slide_push_hammer && !player_input.GetButton(14) && !is_da_pulling_trigger;

			if (_hammer_state == 0 && hammer.amount >= _hammer_halfcocked)
			{
				_hammer_state = 1;
				if (!hammer.asleep && isHammerFree)
				{
					hammer.target_amount = _hammer_halfcocked;
				}
				if (!LocalAimHandler.player_instance.PullingTrigger) AudioManager.PlayOneShotAttached("event:/guns/1911/1911_half_cock", hammer.transform.gameObject);
			}
			if (_hammer_state == 1 && hammer.amount >= _hammer_cocked_val && !IsSafetyOn())
			{
				_hammer_state = 2;
				if (!hammer.asleep && isHammerFree)
				{
					hammer.target_amount = _hammer_cocked_val;
				}
				if (!LocalAimHandler.player_instance.PullingTrigger) AudioManager.PlayOneShotAttached("event:/guns/1911/1911_full_cock", hammer.transform.gameObject);
			}
			if (SearBlocksHammer())
			{
				if (_hammer_state == 1 && isHammerFree)
				{
					hammer.target_amount = _hammer_halfcocked;
					if (!hammer_rest && hammer.amount == _hammer_halfcocked)
					{
						if (IsSafetyOn() && !decocking) AudioManager.PlayOneShotAttached(sound_dry_fire, this.hammer.transform.gameObject, 0.5f);
						AudioManager.PlayOneShotAttached("event:/guns/1911/1911_hammer_rest", this.hammer.transform.gameObject, 2f);
						hammer_rest = true;
					}
				}
				if (_hammer_state == 2 && isHammerFree)
				{
					hammer.target_amount = _hammer_cocked_val;
					if (!hammer_rest && hammer.amount == _hammer_cocked_val)
					{
						AudioManager.PlayOneShotAttached("event:/guns/1911/1911_hammer_rest", this.hammer.transform.gameObject, 2f);
						hammer_rest = true;
					}
				}
			}
		}

		if (!slide_push_hammer && !is_da_pulling_trigger) hammer.TimeStep(Time.deltaTime);
		hammer.UpdateDisplay();

		if (IsSafetyOn() && !slide_push_hammer)
		{ // Safety blocks the trigger from moving, also blocks the hammer for being cocked because for some reason the game still allows for the player to decock??????
			if (_hammer_state == 2) _hammer_state = 1;

			sear.amount = InterpCurve(kSearSafetyCurve, safety.amount); 

			if (hammer.amount > _hammer_halfcocked) hammer.target_amount = Mathf.Max(_hammer_halfcocked, hammer.target_amount);

			da_needs_reset = true;

			trigger.amount = Mathf.Min(trigger.amount, 0.1f);
			trigger.UpdateDisplay();
		}
		else if (!SearBlocksHammer() && hammer.amount >= _hammer_cocked_val && !_disconnector_needs_reset && !player_input.GetButton(RewiredConsts.Action.Hammer) && !slide_push_hammer && !decocking) //hammer piece of shit dickhead firing logic
		{
			if (slide.amount == 0f)
			{
				hammer.target_amount = 0f;
				hammer.vel = -0.1f * ReceiverCoreScript.Instance().player_stats.animation_speed;

				isInDoubleActionMode = false;
			}
		}

		if (slide.amount > 0f)
		{
			_disconnector_needs_reset = true;
		}
		if (slide.amount == 0f && trigger.amount == 0f) //makes it so you have to unpress the trigger to be able to shoot again I think actually I don't know really but it seems like what it is
		{
			da_needs_reset = false;
			if (_disconnector_needs_reset)
			{
				_disconnector_needs_reset = false;
				AudioManager.PlayOneShotAttached(sound_trigger_reset, trigger.transform.gameObject);
			}
		}

		if (hammer.amount == 0f && _hammer_state == 2 && !decocking && !player_input.GetButton(RewiredConsts.Action.Hammer) && !slide_push_hammer) //shooting logic
		{
			da_needs_reset = true;
			TryFireBullet(1);
			_hammer_state = 0;
		}

		sear.UpdateDisplay();
		trigger.UpdateDisplay();
		UpdateAnimatedComponents();
	}

	private bool SearBlocksHammer()
	{
		return sear.amount <= sear_safety_on;
	}

	public override void LateUpdateGun()
	{
		ApplyTransform("slide_stop_tige", slide_stop.amount, transform.Find("sear_and_slide_stop_spring_tige"));

		main_spring.SetBlendShapeWeight(0, magazine_catch.amount * 100f);
		main_spring.SetBlendShapeWeight(1, trigger.amount * 100f);
		main_spring.SetBlendShapeWeight(2, hammer.amount * 100f);

		if (slide_push_hammer && interpCurveSlideHammer > hammer.amount)
		{
			hammer.amount = interpCurveSlideHammer;
			hammer.UpdateDisplay();
		}
	}
}
