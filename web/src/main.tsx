import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { Layout } from './components/Layout'
import './index.css'
import { InvoiceDetailPage } from './pages/InvoiceDetailPage'
import { InvoiceListPage } from './pages/InvoiceListPage'
import { UsagePage } from './pages/UsagePage'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Don't hammer the API with retries on 4xx; one retry covers a brief network blip.
      retry: 1,
      refetchOnWindowFocus: true,
    },
  },
})

const router = createBrowserRouter([
  {
    element: <Layout />,
    children: [
      { path: '/', element: <InvoiceListPage /> },
      { path: '/invoices/:id', element: <InvoiceDetailPage /> },
      { path: '/usage', element: <UsagePage /> },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  </StrictMode>,
)
