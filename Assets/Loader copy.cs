using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Loader : MonoBehaviour
{
    private float _startTime;
    private void Start()
    {
        _startTime = Time.time;
    }
    
    public Image Fill;
    private void Update()
    {
        if(Time.time < _startTime + 2)
            return;
        
        if (Input.anyKeyDown && _async == null)
        {
            _async  = SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Single);

        }

        if (_async != null)
        {
            Fill.fillAmount = _async.progress;
        }
    }

    private AsyncOperation _async;

}