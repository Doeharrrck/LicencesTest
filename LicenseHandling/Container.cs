namespace LicenseHandling
{
    class Pair<T1, T2>
    {
        public T1 First;
        public T2 Second;

        public Pair(T1 First, T2 Second)
        {
            this.First = First;
            this.Second = Second;
        } // Pair()
    } // class Pair


    class Triple<T1, T2, T3>
    {
        public T1 First;
        public T2 Second;
        public T3 Third;

        public Triple(T1 First, T2 Second, T3 Third)
        {
            this.First = First;
            this.Second = Second;
            this.Third = Third;
        } // Triple()
    } // class Triple
}
