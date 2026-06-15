using UnityEngine;

namespace BricksBreakerDemo
{
    public class Brick : MonoBehaviour
    {
        private void OnCollisionEnter2D(Collision2D other) 
        {
            if(other.rigidbody.TryGetComponent<Ball>(out var ball))
            {
                gameObject.SetActive(false);
            }
        }
    }
}
