using Rydo.Application.Admin;
using Rydo.Application.Disputes;
using Rydo.Application.Matching;
using Rydo.Application.Payments;
using Rydo.Application.Realtime;
using Rydo.Application.Trips;

namespace Rydo.Api.Hubs;

public interface IOperationsClient
{
    Task TripUpdated(TripResult trip);

    Task TripOfferUpdated(TripOfferResult offer);

    Task DriverAvailabilityUpdated(DriverAvailabilityResult availability);

    Task PaymentUpdated(PaymentResult payment);

    Task DisputeUpdated(DisputeDetailsResult dispute);

    Task DriverReviewUpdated(DriverReviewChangedResult review);

    Task AdminOperationsChanged(AdminOperationsChangedResult change);
}
