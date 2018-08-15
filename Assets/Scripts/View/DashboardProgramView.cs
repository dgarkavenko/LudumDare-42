﻿using System;
using Model;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace View
{
	public class DashboardProgramView : MonoBehaviour, IDisposable
	{
		[SerializeField] private ProgramView _programView;
		[SerializeField] private Button _upgradeButton;
		[SerializeField] private Button _patchButton;
		[SerializeField] private TextMeshProUGUI _characteristicsLabel;
		[SerializeField] private RectTransform _sizeIndicator;
		[SerializeField] private TextMeshProUGUI _sizeIndicatorLabel;
		[SerializeField] private TextMeshProUGUI _description;

        private CompositeDisposable _disposable;

		public void Show(Program program, Game game, ReactiveProperty<IOperationResult> pendingAction)
		{
            _disposable = new CompositeDisposable();

            _programView.Show(game, program);
			_programView.AddTo(_disposable);

			_description.text = program.CurrentVersion.Value.Description;

			program.CurrentVersion.Subscribe(_ => _upgradeButton.GetComponentInChildren<Text>().text = "v" + (program.GetCurrentVersionIndex() + 2));

			_upgradeButton.OnClickAsObservable().Subscribe(_ => program.Upgrade(game.GameProgress)).AddTo(_disposable);
			_patchButton.OnClickAsObservable().Subscribe(_ => program.Patch(game.GameProgress)).AddTo(_disposable);

			program.CanUpgrade(game.GameProgress).Subscribe(upgradeResult =>
			{
				_upgradeButton.interactable = upgradeResult.Error == null;
				_upgradeButton.gameObject.SetActive(!(upgradeResult.Error is Program.FinalVersionReachedError));
			}).AddTo(_disposable);

			program.CanPatch(game.GameProgress).Subscribe(patchResult =>
			{
				_patchButton.interactable = patchResult.Error == null;
				_patchButton.gameObject.SetActive(!(patchResult.Error is Program.FinalVersionReachedError));
			}).AddTo(_disposable);

			program.LeakBytesPerSecond.CombineLatest(program.ProduceBytesPerSecond,
				(leak, produce) => $"<color=#FBDF6A>produce</color> {produce} byte/s    <color=#BD306C>leak</color> {leak} byte/s")
				.Subscribe(x => _characteristicsLabel.text = x).AddTo(_disposable);

			program.MemorySize.Subscribe(size =>
			{
				_sizeIndicator.sizeDelta = new Vector2(Mathf.FloorToInt(size * game.Template.MemoryIndicationScale), _sizeIndicator.sizeDelta.y);
				_sizeIndicatorLabel.text = $"  {size / 1024}kb   ";
			}).AddTo(_disposable);

			_upgradeButton.gameObject.GetComponent<HoverTrigger>().Hovered
				.Subscribe(hovered => pendingAction.Value = hovered ? program.Upgrade(game.GameProgress, simulate: true).Value : null)
				.AddTo(_disposable);

			_patchButton.gameObject.GetComponent<HoverTrigger>().Hovered
				.Subscribe(hovered => pendingAction.Value = hovered ? program.Patch(game.GameProgress, simulate: true).Value : null)
				.AddTo(_disposable);
		}

		public void Dispose() => _disposable.Dispose();
	}
}
