using UnityEngine;

namespace Tests
{
    public class Commands
    {
        private int seq;
        /*private float horizontal;
        private float vertical;
        private bool space;*/
        private bool up;
        private bool down;
        private bool left;
        private bool right;

        public Commands(int seq, bool up, bool down, bool left, bool right/*float vertical, float horizontal, bool space*/)
        {
            this.seq = seq;
            /*this.vertical = vertical;
            this.horizontal = horizontal;
            this.space = space;*/
            this.up = up;
            this.down = down;
            this.left = left;
            this.right = right;
        }

        public Commands(Commands other)
        {
            seq = other.Seq;
            up = other.Up;
            down = other.Down;
            left = other.Left;
            right = other.Right;
        }

        public Commands()
        {
            seq = 1;
        }

        public bool hasCommand()
        {
            return up || down || left || right; //(vertical != 0f) || (horizontal != 0f) || space;
        }

        public void Reset()
        {
            up = down = left = right = false;
        }

        public int GetHorizontal()
        {
            return (left ? -1 : 0) + (right ? 1 : 0);
        }

        public int GetVertical()
        {
            return (down ? -1 : 0) + (up ? 1 : 0);
        }

        public int Seq
        {
            get => seq;
            set => seq = value;
        }

        public bool Up
        {
            get => up;
            set => up = value;
        }

        public bool Down
        {
            get => down;
            set => down = value;
        }

        public bool Left
        {
            get => left;
            set => left = value;
        }

        public bool Right
        {
            get => right;
            set => right = value;
        }

        /*public float Vertical => vertical;
        
        public float Horizontal => horizontal;

        public bool Space => space;
        
        public override string ToString()
        {
            return $"{nameof(Seq)}: {Seq}, {nameof(Horizontal)}: {Horizontal}, {nameof(Vertical)}: {Vertical}, {nameof(Space)}: {Space}";
        }*/

        public override string ToString()
        {
            return $"{nameof(seq)}: {seq}, {nameof(up)}: {up}, {nameof(down)}: {down}, {nameof(left)}: {left}, {nameof(right)}: {right}";
        }
    }
}