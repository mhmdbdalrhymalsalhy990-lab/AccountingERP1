# Multi-stage Dockerfile for Accounting ERP UI (Angular 21+ & Nginx)
# Stage 1: Build the Angular application
FROM node:24-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

# Stage 2: Serve using Nginx
FROM nginx:alpine
COPY --from=build /app/dist/accounting-erp-ui /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
