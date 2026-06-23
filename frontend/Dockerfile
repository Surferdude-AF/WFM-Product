# Dev/demo image: build the SPA and serve it with Vite's preview server. The
# browser still calls the API at http://localhost:8080 (the published API port),
# so CORS (Development) handles the cross-origin call.
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build
EXPOSE 5173
CMD ["npm", "run", "preview", "--", "--host", "0.0.0.0", "--port", "5173"]
