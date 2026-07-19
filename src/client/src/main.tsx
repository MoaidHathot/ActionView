import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { ActionJobsProvider } from './context/ActionJobsProvider'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ActionJobsProvider>
      <App />
    </ActionJobsProvider>
  </StrictMode>,
)
