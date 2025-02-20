﻿namespace CelesteStudio.RichText {
    public struct Place {
        public int iChar;
        public int iLine;

        public Place(int iChar, int iLine) {
            this.iChar = iChar;
            this.iLine = iLine;
        }

        public void Offset(int dx, int dy) {
            iChar += dx;
            iLine += dy;
        }

        public override int GetHashCode() {
            return iChar ^ iLine;
        }

        public override bool Equals(object obj) {
            return obj is Place && ((Place) obj).iLine == iLine && ((Place) obj).iChar == iChar;
        }

        public static bool operator !=(Place p1, Place p2) {
            return !p1.Equals(p2);
        }

        public static bool operator ==(Place p1, Place p2) {
            return p1.Equals(p2);
        }

        public static bool operator <(Place p1, Place p2) {
            if (p1.iLine < p2.iLine) {
                return true;
            }

            if (p1.iLine > p2.iLine) {
                return false;
            }

            if (p1.iChar < p2.iChar) {
                return true;
            }

            return false;
        }

        public static bool operator <=(Place p1, Place p2) {
            if (p1.Equals(p2)) {
                return true;
            }

            if (p1.iLine < p2.iLine) {
                return true;
            }

            if (p1.iLine > p2.iLine) {
                return false;
            }

            if (p1.iChar < p2.iChar) {
                return true;
            }

            return false;
        }

        public static bool operator >(Place p1, Place p2) {
            if (p1.iLine > p2.iLine) {
                return true;
            }

            if (p1.iLine < p2.iLine) {
                return false;
            }

            if (p1.iChar > p2.iChar) {
                return true;
            }

            return false;
        }

        public static bool operator >=(Place p1, Place p2) {
            if (p1.Equals(p2)) {
                return true;
            }

            if (p1.iLine > p2.iLine) {
                return true;
            }

            if (p1.iLine < p2.iLine) {
                return false;
            }

            if (p1.iChar > p2.iChar) {
                return true;
            }

            return false;
        }

        public static Place Empty => new();

        public override string ToString() {
            return "(" + iChar + "," + iLine + ")";
        }
    }
}