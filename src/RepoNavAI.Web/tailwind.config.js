/** @type {import('tailwindcss').Config} */
export default { content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'], theme: { extend: { colors: { ink: '#111827', canvas: '#f5f7fb', brand: { 50: '#eef6ff', 100: '#d9ebff', 500: '#2574d8', 600: '#185fba', 700: '#164c91' } }, boxShadow: { panel: '0 24px 60px -32px rgba(15,23,42,.35)' } } }, plugins: [] };
