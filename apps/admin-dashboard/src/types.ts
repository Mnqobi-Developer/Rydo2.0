export type AdminSession = {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  user: { id: string; phoneNumber: string; role: 'Admin' }
}

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export type AdminOverview = {
  passengerCount: number
  driverCount: number
  pendingDriverCount: number
  activeTripCount: number
  awaitingPaymentCount: number
  openDisputeCount: number
  onlineDriverCount: number
}

export type DriverStatus = 'Draft' | 'PendingReview' | 'Approved' | 'Rejected'
export type ReviewStatus = 'PendingReview' | 'Approved' | 'Rejected'

export type DriverDocument = {
  id: string
  documentType: 'IdentityDocument' | 'DriversLicense' | 'ProfessionalDrivingPermit'
  originalFileName: string
  contentType: string
  sizeBytes: number
  sha256: string
  reviewStatus: ReviewStatus
  uploadedAt: string
  reviewedAt: string | null
  rejectionReason: string | null
}

export type DriverApplication = {
  profile: {
    userId: string
    firstName: string
    lastName: string
    email: string | null
    onboardingStatus: DriverStatus
    canEdit: boolean
    createdAt: string
    updatedAt: string
    submittedAt: string | null
    reviewedAt: string | null
    rejectionReason: string | null
  }
  documents: DriverDocument[]
  vehicle: null | {
    id: string
    make: string
    model: string
    year: number
    color: string
    registrationNumber: string
    vehicleIdentificationNumber: string
    seatCapacity: number
    reviewStatus: ReviewStatus
    rejectionReason: string | null
  }
}

export type AdminUser = {
  id: string
  phoneNumber: string
  role: 'Passenger' | 'Driver' | 'Admin'
  isActive: boolean
  displayName: string | null
  createdAt: string
}

export type Trip = {
  id: string
  passengerUserId: string
  driverUserId: string | null
  pickupAddress: string
  pickupLatitude: number
  pickupLongitude: number
  destinationAddress: string
  destinationLatitude: number
  destinationLongitude: number
  rideCategory: string | null
  estimatedFareAmount: number | null
  fareCurrency: string | null
  status: 'Requested' | 'Accepted' | 'DriverArrived' | 'InProgress' | 'Completed' | 'Cancelled'
  requestedAt: string
  updatedAt: string
  finalFareAmount: number | null
}

export type LiveDriver = {
  driverUserId: string
  displayName: string
  latitude: number
  longitude: number
  locationUpdatedAt: string | null
}

export type Payment = {
  id: string
  tripId: string
  passengerUserId: string
  method: 'Cash' | 'PayFast'
  status: 'AwaitingPayment' | 'Paid' | 'Cancelled' | 'Failed'
  amount: number
  currency: string
  providerPaymentId: string | null
  createdAt: string
  updatedAt: string
  failureReason: string | null
}

export type Dispute = {
  dispute: {
    id: string
    tripId: string
    openedByUserId: string
    category: string
    subject: string
    description: string
    status: 'Open' | 'UnderReview' | 'Resolved' | 'Rejected'
    createdAt: string
    updatedAt: string
    resolution: string | null
    messages: { id: string; authorUserId: string; body: string; createdAt: string }[]
  }
  passengerUserId: string
  driverUserId: string | null
}

export type AuditEntry = {
  id: string
  adminUserId: string
  action: string
  entityType: string
  entityId: string
  details: string
  createdAt: string
}

export type DashboardData = {
  overview: AdminOverview
  drivers: PagedResult<DriverApplication>
  users: PagedResult<AdminUser>
  trips: PagedResult<Trip>
  payments: PagedResult<Payment>
  liveDrivers: LiveDriver[]
  disputes: PagedResult<Dispute>
  audit: PagedResult<AuditEntry>
}
