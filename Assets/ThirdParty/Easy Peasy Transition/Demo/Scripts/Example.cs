namespace EasyPeasyTransition
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;

    public class Example : MonoBehaviour
    {
        public List<EasyPeasyTransition.TransitionType> transitionTypes;
        public Transform buttonGroup;
        public Button buttonBase;
        public string targetScene = string.Empty;

        private void Test()
        {
            EasyPeasyTransition.Instance.PlayTransition(EasyPeasyTransition.TransitionType.Fade);
            EasyPeasyTransition.Instance.LoadScene("Scene2", EasyPeasyTransition.TransitionType.HorizontalBlinds);
        }
        void Start()
        {
            transitionTypes.Clear();
            var allTransitions = Enum.GetValues(typeof(EasyPeasyTransition.TransitionType));

            foreach (EasyPeasyTransition.TransitionType type in allTransitions)
            {
                transitionTypes.Add(type);
                var button = Instantiate(buttonBase, buttonGroup);
                Text text = button.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = type.ToString();
                }
                button.onClick.AddListener(delegate
                {
                    if (!targetScene.Equals(string.Empty))
                    {
                        EasyPeasyTransition.Instance.LoadScene(targetScene, type);
                    }
                    else
                    {
                        EasyPeasyTransition.Instance.PlayTransition(type, 1f, 1f, Color.black, OnTransitionHalfway, OnTransitionEnded);
                        buttonGroup.gameObject.SetActive(false);
                    }
                });
            }
        }
        private void OnTransitionHalfway()
        {

        }
        private void OnTransitionEnded()
        {
            if (buttonGroup)
                buttonGroup.gameObject.SetActive(true);
        }
    }
}
