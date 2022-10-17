using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Ark : MonoBehaviour
{

	public GameObject[] Stages;
	public Action<float> OnRecycle;
	public AudioSource Source;
	public AudioClip Clip;

	public ParticleSystem PS;

	private float _perStage;
	// Use this for initialization
	void Start ()
	{
		_perStage = 1f / (Stages.Length - 1);
		UpdateStage(0);
	}

	public void UpdateStage(float f)
	{
		int stage = (int)(f / _perStage);

		stage = Mathf.Min(Stages.Length - 1, stage);

		for (int i = 0; i < Stages.Length; i++)
			Stages[i].SetActive(i == stage);
	}

    public void LoadTrunk(TreeTrunk trunk)
    {
        trunk.IsLoaded = true;
    }

	private void OnTriggerEnter(Collider other)
	{

		var player = other.gameObject.GetComponent<PlayerController>();

		var treetrunk = other.gameObject.GetComponent<TreeTrunk>();
		if (treetrunk != null)
		{
			treetrunk.RecyclingStarted += RecyclingStarted;
			treetrunk.Recycle(this);
			
			
		}
	}

	private void RecyclingStarted(TreeTrunk obj, float ln)
	{
		Source.PlayOneShot(Clip);
		StartCoroutine(Recycle(obj.Wood, ln, obj.transform));

	}

	IEnumerator Recycle(float val, float t, Transform tr)
	{
		t /= 2f;
		var perQuarterSec = val / t / 4f;

		var position = tr.position;
		var dir = tr.forward;
		while (val > 0)
		{
			if (OnRecycle != null)
				OnRecycle(Mathf.Min(val, perQuarterSec));

			val -= perQuarterSec;
			
			
			yield return new WaitForSeconds(.25f);


			PS.transform.position = position + dir * 2.5f /*+ PS.transform.TransformPoint(0, 0, Random.Range(0, 4))*/;
			//var d = transform.position - PS.transform.position;

			//PS.transform.LookAt(PS.transform.position + dir.normalized);
			PS.Emit( (int)Mathf.Max(4, perQuarterSec / 4f));
		}
	}
}
