﻿using System.Collections;
using System.Collections.Generic;
using Model;
using UnityEngine;

public class MainApplication : MonoBehaviour
{
    private static MainApplication _instance;
    public static MainApplication Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<MainApplication>();
                if (_instance == null)
                {
                    var go = new GameObject("___Application");
                    _instance = go.AddComponent<MainApplication>();
                }
            }

            return _instance;
        }
    }

    public Game Game;

    public LayerMask SelectableObjects;

    private InputManager _input;
    public PlayerController CurrentPlayer;
    public Transform PointToChillAround;

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
        if (_instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        var inputs = FindObjectsOfType<InputManager>();
        for (int i = 0; i < inputs.Length; i++)
        {
            if (inputs[i].gameObject != gameObject)
                Destroy(inputs[i].gameObject);
            else
                _input = inputs[i];
        }

        if (_input == null)
            _input = gameObject.AddComponent<InputManager>();

        CurrentPlayer = FindObjectOfType<PlayerController>();
        if (CurrentPlayer == null)
        {
            var playerObj = new GameObject();
            CurrentPlayer = playerObj.AddComponent<PlayerController>();
            CurrentPlayer.Init();
        }
    }

    public void MoveMainCharacter(Vector2 movement)
    {
        CurrentPlayer.Move(movement);
    }

    public void SelectObject(Selectable selected)
    {
        Game.SelectedRobot.Value = selected?.GetComponent<RobotController>()?.RobotModel;
    }

    public void MainCharacterSpin()
    {
        CurrentPlayer.Spin();
    }

    public void MainCharacterDrag()
    {
        CurrentPlayer.FindDragTarget();
    }
}
