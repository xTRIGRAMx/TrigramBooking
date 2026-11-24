namespace TrigramBooking.API.Services.Results
{
    public class ServiceResult<T>
    {
        public bool Success { get; private set; }

        public T? Data { get; private set; }

        public ServiceError? Error { get; private set; }

        private ServiceResult() { }

        public static ServiceResult<T> Successful(T data)
        {
            return new ServiceResult<T>
            {
                Success = true,
                Data = data
            };
        }

        public static ServiceResult<T> Failed(ServiceErrorCode code, string message)
        {
            return new ServiceResult<T>
            {
                Success = false,
                Error = new ServiceError
                {
                    Code = code,
                    Message = message
                }
            };
        }
    }
}
