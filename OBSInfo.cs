
namespace NeonCapture
{
    public static class OBSInfo
    {
        public enum Opcodes
        {
            Hello = 0,
            Identify = 1,
            Identified = 2,
            Reidentify = 3,
            Event = 5,
            Request = 6,
            RequestResponse = 7,
            RequestBatch = 8,
            RequestBatchResponse = 9
        }

        public enum CloseCode
        {
            DontClose = 0,
            UnknownReason = 4000,
            MessageDecodeError = 4002,
            MissingDataField = 4003,
            InvalidDataFieldType = 4004,
            InvalidDataFieldValue = 4005,
            UnknownOpCode = 4006,
            NotIdentified = 4007,
            AlreadyIdentified = 4008,
            AuthenticationFailed = 4009,
            UnsupportedRpcVersion = 4010,
            SessionInvalidated = 4011,
            UnsupportedFeature = 4012
        }
        public enum RequestStatus
        {
            Unknown = 0,
            NoError = 10,
            Success = 100,
            MissingRequestType = 203,
            UnknownRequestType = 204,
            GenericError = 205,
            UnsupportedRequestBatchExecutionType = 206,
            NotReady = 207,
            MissingRequestField = 300,
            MissingRequestData = 301,
            InvalidRequestField = 400,
            InvalidRequestFieldType = 401,
            RequestFieldOutOfRange = 402,
            RequestFieldEmpty = 403,
            TooManyRequestFields = 404,
            OutputRunning = 500,
            OutputNotRunning = 501,
            OutputPaused = 502,
            OutputNotPaused = 503,
            OutputDisabled = 504,
            StudioModeActive = 505,
            StudioModeNotActive = 506,
            ResourceNotFound = 600,
            ResourceAlreadyExists = 601,
            InvalidResourceType = 602,
            NotEnoughResources = 603,
            InvalidResourceState = 604,
            InvalidInputKind = 605,
            ResourceNotConfigurable = 606,
            InvalidFilterKind = 607,
            ResourceCreationFailed = 700,
            ResourceActionFailed = 701,
            RequestProcessingFailed = 702,
            CannotAct = 703
        }
    }

}