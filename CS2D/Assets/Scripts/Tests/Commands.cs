using UnityEngine;

namespace Tests
{
    public class Commands
    {
        private static int _seq = 0;
        private int seq;
        private float horizontal;
        private float vertical;
        private bool space;

        public Commands(float vertical, float horizontal, bool space)
        {
            this.seq = _seq;
            this.vertical = vertical;
            //this.up = up;
            //this.down = down;
            this.horizontal = horizontal;
            //this.right = right;
            //this.left = left;
            this.space = space;
            if (hasCommand())
                _seq++;
        }

        /*public override string ToString()
        {
            return $"{nameof(Seq)}: {Seq}, {nameof(Up)}: {Up}, {nameof(Down)}: {Down}, {nameof(Right)}: {Right}, {nameof(Left)}: {Left}, {nameof(Space)}: {Space}";
        }*/

        public Commands(int seq, float vertical, float horizontal, bool space)
        {
            this.seq = seq;
            this.vertical = vertical;
            //this.up = up;
            //this.down = down;
            this.horizontal = horizontal;
            //this.right = right;
            //this.left = left;
            this.space = space;
        }

        public bool hasCommand()
        {
            //return up || down || right || left || space;
            return (vertical != 0f) || (horizontal != 0f) || space;
        }

        public int Seq => seq;

        public float Vertical => vertical;

        //public bool Up => up;

        //public bool Down => down;
        
        public float Horizontal => horizontal;

        //public bool Right => right;

        //public bool Left => left;

        public bool Space => space;
    }
}