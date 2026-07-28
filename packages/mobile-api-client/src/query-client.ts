export const mobileQueryClientOptions = {
  defaultOptions: {
    queries: {
      gcTime: 10 * 60 * 1000,
      staleTime: 30 * 1000,
      retry: false,
      refetchOnReconnect: true,
    },
    mutations: {
      retry: false,
    },
  },
} as const;
