namespace RimWorld_ModSyncNinja
{
    class Spinner
    {
        private int dots = 0;
        private int frame = 0;

        public string GetSpinnerDots()
        {
            // ◰ ◳ ◲ ◱
            switch (this.dots)
            {
                case 1:
                    return @"   ";
                case 2:
                    return @".  ";
                case 3:
                    return @".. ";
                case 4:
                    return @"...";
                case 5:
                    return @".. ";
                case 6:
                    return @".  ";
            }
            return "";
        }

        public void OnDoWindowContents()
        {
            if (this.frame > 60)
            {
                if (this.dots > 5)
                    this.dots = 0;
                ++this.dots;

                this.frame = 0;
            }
            else
                this.frame++;
        }
    }
}
