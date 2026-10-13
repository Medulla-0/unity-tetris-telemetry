using UnityEngine;
using TMPro;
using UnityEngine.Events;
using System;
using UnityEngine.UI;

public class GameEvent : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Game Start");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGame();
        }
    }
}
