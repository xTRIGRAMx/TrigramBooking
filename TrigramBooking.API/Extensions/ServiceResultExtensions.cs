using Microsoft.AspNetCore.Mvc;
using TrigramBooking.API.Services.Results;

namespace TrigramBooking.API.Extensions
{
    public static class ServiceResultExtensions
    {
        public static IActionResult ToErrorResponse<T>(this ServiceResult<T> result)
        {
            ServiceError error = result.Error!;

            switch (error.Code)
            {
                case ServiceErrorCode.NotFound:
                    return new NotFoundObjectResult(new { Message = error.Message });

                case ServiceErrorCode.AlreadyExists:
                    return new ConflictObjectResult(new { Message = error.Message });

                case ServiceErrorCode.Conflict:
                    return new ConflictObjectResult(new { Message = error.Message });

                case ServiceErrorCode.InvalidRequest:
                    return new BadRequestObjectResult(new { Message = error.Message });

                case ServiceErrorCode.Unauthorized:
                    return new UnauthorizedObjectResult(new { Message = error.Message });

                case ServiceErrorCode.Forbidden:
                    return new ObjectResult(new { Message = error.Message })
                    {
                        StatusCode = 403
                    };

                default:
                    return new ObjectResult(new { Message = "An unexpected error occurred." })
                    {
                        StatusCode = 500
                    };
            }
        }
    }
}
