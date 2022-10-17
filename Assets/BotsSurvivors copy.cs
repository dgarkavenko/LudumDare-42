using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BotsSurvivors : MonoBehaviour
{

	public static int Survived = 2;
	public GameObject[] Survivors;

	public void Awake()
	{
		for (int i = 0; i < Survivors.Length; i++)
			Survivors[i].SetActive(i < Survived);
	}
}
