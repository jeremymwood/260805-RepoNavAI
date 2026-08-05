FROM node:22-alpine AS build
WORKDIR /app
COPY src/RepoNavAI.Web/package*.json ./
RUN npm ci
COPY src/RepoNavAI.Web/ .
RUN npm run build
FROM nginx:1.27-alpine
COPY src/RepoNavAI.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
