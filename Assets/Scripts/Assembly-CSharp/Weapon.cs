using System;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
	public enum Effectiveness
	{
		No = 0,
		Yes = 1,
		Preferred = 2
	}

	[Serializable]
	public class Configuration
	{
		public bool auto;

		public int ammo = 10;

		public int spareAmmo = 50;

		public int resupplyNumber = 10;

		public float reloadTime = 2f;

		public float cooldown = 0.2f;

		public float unholsterTime = 1.2f;

		public float aimFov = 50f;

		public bool loud = true;

		public Transform muzzle;

		public ParticleSystem[] muzzleFlash;

		public ParticleSystem casing;

		public int projectilesPerShot = 1;

		public GameObject projectilePrefab;

		public float kickback = 2f;

		public float randomKick = 0.2f;

		public float spread;

		public float snapMagnitude = 0.3f;

		public float snapDuration = 0.4f;

		public float snapFrequency = 4f;

		public bool aiIgnoreFriendlies;

		public float aiAllowedAimSpread = 1f;

		public Effectiveness effInfantry = Effectiveness.Yes;

		public Effectiveness effInfantryGroup;

		public Effectiveness effUnarmored = Effectiveness.Yes;

		public Effectiveness effArmored;

		public Effectiveness effAir;

		public float effectiveRange = 100f;
	}

	[NonSerialized]
	public Actor user;

	public Transform thirdPersonTransform;

	public Vector3 thirdPersonOffset = Vector3.zero;

	public Configuration configuration;

	public Sprite uiSprite;

	[NonSerialized]
	public int ammo;

	[NonSerialized]
	public bool reloading;

	protected float lastFired;

	protected bool holdingFire;

	protected bool unholstered;

	protected AudioSource audio;

	protected float weaponVolume = 1f;

	protected Action stopFireLoop = new Action(0.12f);

	[NonSerialized]
	public float projectileSpeed;

	[NonSerialized]
	public Animator animator;

	[NonSerialized]
	public int slot = -1;

	protected List<Renderer> renderers;

	protected virtual void Awake()
	{
		if (configuration.projectilePrefab != null)
		{
			projectileSpeed = configuration.projectilePrefab.GetComponent<Projectile>().configuration.speed;
		}
		else
		{
			projectileSpeed = 100f;
		}
		animator = GetComponent<Animator>();
	}

	protected virtual void Start()
	{
		audio = GetComponent<AudioSource>();
		weaponVolume = audio.volume;
		audio.loop = configuration.auto;
		ammo = configuration.ammo;
	}

	public virtual void FindRenderers(bool thirdperson)
	{
		if (thirdperson)
		{
			renderers = new List<Renderer>(thirdPersonTransform.GetComponentsInChildren<Renderer>());
		}
		else
		{
			renderers = new List<Renderer>(GetComponentsInChildren<Renderer>());
		}
	}

	protected virtual void Update()
	{
		if (!stopFireLoop.Done())
		{
			audio.volume = (1f - stopFireLoop.Ratio()) * weaponVolume;
			if (stopFireLoop.TrueDone())
			{
				audio.Stop();
			}
		}
	}

	public virtual void Fire(Vector3 direction, bool useMuzzleDirection)
	{
		if (CanFire())
		{
			if (configuration.auto && (!audio.isPlaying || !stopFireLoop.Done()))
			{
				StartFireLoop();
			}
			Shoot(direction, useMuzzleDirection);
		}
		holdingFire = true;
	}

	private void StartFireLoop()
	{
		audio.volume = weaponVolume;
		audio.Play();
		stopFireLoop.Stop();
	}

	private void StopFireLoop()
	{
		stopFireLoop.Start();
	}

	public void StopFire()
	{
		if (configuration.auto)
		{
			StopFireLoop();
		}
		holdingFire = false;
	}

	public virtual void SetAiming(bool aiming)
	{
		if (HasActiveAnimator())
		{
			animator.SetBool("aim", aiming);
		}
	}

	public virtual void Reload()
	{
		if (unholstered && !reloading)
		{
			if (HasActiveAnimator())
			{
				animator.SetTrigger("reload");
			}
			DisableOverrideLayer();
			reloading = true;
			Invoke("ReloadDone", configuration.reloadTime);
		}
	}

	protected void ReloadDone()
	{
		EnableOverrideLayer();
		reloading = false;
		int count = configuration.ammo - ammo;
		int num = RemoveSpareAmmo(count);
		ammo += num;
		AmmoChanged();
	}

	protected virtual int RemoveSpareAmmo(int count)
	{
		return user.RemoveSpareAmmo(count, slot);
	}

	private void AmmoChanged()
	{
		user.AmmoChanged();
		if (HasActiveAnimator())
		{
			animator.SetBool("no ammo", !HasAnyAmmo());
		}
	}

	private void DisableOverrideLayer()
	{
		if (HasActiveAnimator() && animator.layerCount > 1)
		{
			animator.SetLayerWeight(1, 0f);
		}
	}

	private void EnableOverrideLayer()
	{
		if (HasActiveAnimator() && animator.layerCount > 1)
		{
			animator.SetLayerWeight(1, 1f);
		}
	}

	public virtual bool CanFire()
	{
		return unholstered && !reloading && HasLoadedAmmo() && (configuration.auto || !holdingFire) && !CoolingDown();
	}

	public bool CoolingDown()
	{
		return Time.time - lastFired < configuration.cooldown;
	}

	public bool AmmoFull()
	{
		return ammo >= configuration.ammo;
	}

	protected virtual void Shoot(Vector3 direction, bool useMuzzleDirection)
	{
		if (configuration.loud)
		{
			user.Highlight();
		}
		if (useMuzzleDirection)
		{
			direction = configuration.muzzle.forward;
		}
		lastFired = Time.time;
		if (HasActiveAnimator())
		{
			animator.SetTrigger("fire");
		}
		for (int i = 0; i < configuration.projectilesPerShot; i++)
		{
			SpawnProjectile(direction);
		}
		ParticleSystem[] muzzleFlash = configuration.muzzleFlash;
		foreach (ParticleSystem particleSystem in muzzleFlash)
		{
			particleSystem.Play();
		}
		ammo--;
		user.ApplyRecoil(configuration.kickback * Vector3.back + UnityEngine.Random.insideUnitSphere * configuration.randomKick);
		AmmoChanged();
		if (!user.aiControlled && configuration.casing != null)
		{
			configuration.casing.Play();
		}
		if (!configuration.auto)
		{
			audio.Play();
		}
		else if (ammo == 0)
		{
			StopFireLoop();
		}
	}

	protected bool HasActiveAnimator()
	{
		return animator != null && animator.isActiveAndEnabled;
	}

	protected virtual void SpawnProjectile(Vector3 direction)
	{
		Quaternion rotation = Quaternion.LookRotation(direction + UnityEngine.Random.insideUnitSphere * configuration.spread);
		((GameObject)UnityEngine.Object.Instantiate(configuration.projectilePrefab, configuration.muzzle.position, rotation)).GetComponent<Projectile>().source = user;
	}

	public virtual void Hide()
	{
		foreach (Renderer renderer in renderers)
		{
			renderer.enabled = false;
		}
	}

	public virtual void Show()
	{
		foreach (Renderer renderer in renderers)
		{
			renderer.enabled = true;
		}
	}

	public void CullFpsObjects()
	{
		for (int i = 0; i < base.transform.childCount; i++)
		{
			Transform child = base.transform.GetChild(i);
			if (child != thirdPersonTransform)
			{
				if (child == configuration.muzzle)
				{
					child.transform.localPosition = thirdPersonTransform.localPosition;
					thirdPersonTransform.localRotation = Quaternion.identity;
				}
				else
				{
					UnityEngine.Object.Destroy(child.gameObject);
				}
			}
		}
	}

	public bool IsEmpty()
	{
		return ammo == 0;
	}

	public void Equip(Actor user)
	{
		this.user = user;
	}

	public void Drop()
	{
		user = null;
		holdingFire = false;
		reloading = false;
		CancelInvoke();
		UnityEngine.Object.Destroy(base.gameObject);
	}

	public virtual void Unholster()
	{
		unholstered = false;
		if (HasActiveAnimator())
		{
			animator.SetBool("no ammo", !HasAnyAmmo());
			animator.SetTrigger("unholster");
		}
		Show();
		DisableOverrideLayer();
		Invoke("UnholsterDone", configuration.unholsterTime);
	}

	public void UnholsterDone()
	{
		EnableOverrideLayer();
		unholstered = true;
	}

	public virtual void Holster()
	{
		unholstered = false;
		reloading = false;
		CancelInvoke();
		base.gameObject.SetActive(false);
	}

	public Effectiveness EffectivenessAgainst(Actor.TargetType targetType)
	{
		switch (targetType)
		{
		case Actor.TargetType.Unarmored:
			return configuration.effUnarmored;
		case Actor.TargetType.Armored:
			return configuration.effArmored;
		case Actor.TargetType.Air:
			return configuration.effAir;
		case Actor.TargetType.InfantryGroup:
			return configuration.effInfantryGroup;
		default:
			return configuration.effInfantry;
		}
	}

	public virtual Vector3 MuzzlePosition()
	{
		return configuration.muzzle.position;
	}

	public bool EffectiveAtRange(float range)
	{
		return configuration.effectiveRange > range;
	}

	public bool AllowsResupply()
	{
		return configuration.spareAmmo != -1;
	}

	public bool HasSpareAmmo()
	{
		if (HasInfiniteSpareAmmo())
		{
			return true;
		}
		return GetSpareAmmo() > 0;
	}

	public bool HasLoadedAmmo()
	{
		return ammo > 0;
	}

	public bool HasAnyAmmo()
	{
		return HasLoadedAmmo() || HasSpareAmmo();
	}

	public bool HasInfiniteSpareAmmo()
	{
		return configuration.spareAmmo == -2;
	}

	public virtual int GetSpareAmmo()
	{
		if (user != null)
		{
			return user.RemainingSpareAmmoFor(this);
		}
		return 0;
	}
}
