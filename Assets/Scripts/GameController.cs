﻿using Data;
using Model;
using UniRx;
using UnityEngine;
using View;

public class GameController : MonoBehaviour
{
    private CompositeDisposable _disposable;

	[SerializeField] private RobotView _robotView;

    [SerializeField] private Dashboard _dashboard;

	[SerializeField] private GameProgressTemplate _defaultGameProgress;
	[SerializeField] private RobotTemplate _defaultRobotTemplate;

	[SerializeField] private RobotController _robotControllerTemplate;
	[SerializeField] private Transform _robotSpawnPosition;

    [SerializeField] private GameTemplate _gameTemplate;

	private void Start()
	{
        _disposable = new CompositeDisposable();

		var game = new Game(_gameTemplate, new GameProgress(_defaultGameProgress));

		MainApplication.Instance.Game = game;

		game.SelectedRobot.Subscribe(robot =>
		{
			if (robot != null && !_robotView.gameObject.activeSelf)
				_robotView.Show(game, game.Robots[0], robot.Transform, Camera.main);
			else if (robot == null && _robotView.gameObject.activeSelf)
				_robotView.Dispose();
		}).AddTo(_disposable);

        _dashboard.Show(game);
        _dashboard.AddTo(_disposable);

		game.Robots.ObserveAdd().Subscribe(addRobot =>
		{
			var robotController = Instantiate(_robotControllerTemplate);
			robotController.RobotModel = addRobot.Value;
			robotController.Game = game;
			robotController.RobotModel.Transform = robotController.transform;
			robotController.transform.position = _robotSpawnPosition.position;
		}).AddTo(_disposable);

		game.Robots.Add(new Robot(_defaultRobotTemplate, game.GameProgress));
	}

    //private void OnDestroy() => _disposable.Dispose();
}