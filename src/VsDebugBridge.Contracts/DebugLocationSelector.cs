namespace VsDebugBridge.Contracts
{
    public static class DebugLocationSelector
    {
        public static DocumentLocation? SelectCurrentLocation(
            DebuggerState debuggerState,
            DocumentLocation? activeDocumentLocation,
            DocumentLocation? stackFrameLocation)
        {
            if (debuggerState != DebuggerState.Break)
            {
                return Clone(activeDocumentLocation);
            }

            if (stackFrameLocation == null)
            {
                return Clone(activeDocumentLocation);
            }

            var location = Clone(stackFrameLocation);
            if (IsSameLocation(stackFrameLocation, activeDocumentLocation))
            {
                location!.Column = activeDocumentLocation!.Column;
            }

            return location;
        }

        private static bool IsSameLocation(DocumentLocation first, DocumentLocation? second)
        {
            if (second == null || first.Line == null || second.Line == null)
            {
                return false;
            }

            return first.Line == second.Line &&
                string.Equals(first.FilePath, second.FilePath, System.StringComparison.OrdinalIgnoreCase);
        }

        private static DocumentLocation? Clone(DocumentLocation? location)
        {
            if (location == null)
            {
                return null;
            }

            return new DocumentLocation
            {
                FilePath = location.FilePath,
                Line = location.Line,
                Column = location.Column
            };
        }
    }
}
