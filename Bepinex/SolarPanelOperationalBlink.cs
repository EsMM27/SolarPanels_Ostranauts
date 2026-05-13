using System;
using System.Reflection;
using UnityEngine;

namespace SolarPanels
{
    public class SolarPanelOperationalBlink : MonoBehaviour
    {
        private static readonly FieldInfo BlinkField = typeof(Item).GetField("bBlink", BindingFlags.Instance | BindingFlags.NonPublic);

        private Item item;
        private bool isOperational;

        protected virtual double OperationalBlinkCycleSeconds => 4.0;
        protected virtual double OperationalBlinkPulseSeconds => 0.6;

        private void Awake()
        {
            item = GetComponent<Item>();
        }

        private void Update()
        {
            if (!isOperational)
            {
                ApplyBlinkState(false);
                return;
            }

            double cycleSeconds = Math.Max(OperationalBlinkCycleSeconds, 0.1);
            double pulseSeconds = Mathf.Clamp((float)OperationalBlinkPulseSeconds, 0.05f, (float)cycleSeconds);
            double phase = StarSystem.fEpoch % cycleSeconds;
            ApplyBlinkState(phase <= pulseSeconds);
        }

        public void SetOperational(bool operational)
        {
            isOperational = operational;
            if (!operational)
            {
                ApplyBlinkState(false);
            }
        }

        private void ApplyBlinkState(bool blinking)
        {
            if (BlinkField == null)
            {
                return;
            }

            if (item == null || item.gameObject != gameObject)
            {
                item = GetComponent<Item>();
            }

            if (item == null)
            {
                return;
            }

            BlinkField.SetValue(item, blinking);
        }
    }
}
