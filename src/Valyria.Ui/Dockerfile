FROM node:20-alpine as build

WORKDIR /app

COPY package.json package.json
COPY package-lock.json package-lock.json

RUN npm install --legacy-peer-deps

COPY . .

RUN npm run build

FROM node:20-alpine

COPY --from=build /app/.output ./

# Expose the default nginx port
EXPOSE 3000

CMD ["node", "server/index.mjs"]