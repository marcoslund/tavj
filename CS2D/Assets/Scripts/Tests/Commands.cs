using UnityEngine;

namespace Tests
{
    public class Commands
    {
        private int seq;
        private float horizontal;
        private float vertical;
        private bool space;

        public Commands(int seq, float vertical, float horizontal, bool space)
        {
            this.seq = seq;
            this.vertical = vertical;
            this.horizontal = horizontal;
            this.space = space;
        }

        public bool hasCommand()
        {
            return (vertical != 0f) || (horizontal != 0f) || space;
        }

        public int Seq => seq;

        public float Vertical => vertical;
        
        public float Horizontal => horizontal;

        public bool Space => space;
        
        public override string ToString()
        {
            return $"{nameof(Seq)}: {Seq}, {nameof(Horizontal)}: {Horizontal}, {nameof(Vertical)}: {Vertical}, {nameof(Space)}: {Space}";
        }
    }
}